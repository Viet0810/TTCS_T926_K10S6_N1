using System.Security.Cryptography;
using System.Text;
using InternManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace InternManagement.Controllers;

[ApiController]
[Route("api")]
public sealed class InternContractsController : ControllerBase
{
    private const long MaxContractSize = 10 * 1024 * 1024;
    private readonly string connectionString;
    private readonly string storageDirectory;
    private readonly AuthTokenService tokens;

    public InternContractsController(IConfiguration configuration, IWebHostEnvironment environment, AuthTokenService tokens)
    {
        connectionString = configuration.GetConnectionString("InternManagement")
            ?? throw new InvalidOperationException("ConnectionStrings:InternManagement is missing.");
        storageDirectory = Path.Combine(environment.ContentRootPath, "App_Data", "contracts");
        this.tokens = tokens;
    }

    [HttpPost("interns/{internId:int}/contract")]
    [RequestSizeLimit(MaxContractSize + 256 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxContractSize + 256 * 1024)]
    public async Task<IActionResult> Upload(int internId, [FromForm] IFormFile? file, CancellationToken ct)
    {
        var denied = Authorize(out var failure);
        if (denied) return failure!;
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Vui lòng chọn tệp hợp đồng PDF." });
        if (file.Length > MaxContractSize)
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new { message = "Tệp hợp đồng vượt quá giới hạn 10 MiB." });
        var extension = Path.GetExtension(Path.GetFileName(file.FileName));
        if (!string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Chỉ chấp nhận hợp đồng định dạng PDF." });
        if (!string.IsNullOrWhiteSpace(file.ContentType)
            && !file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
            && !file.ContentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "MIME type không phù hợp với tệp PDF." });

        Directory.CreateDirectory(storageDirectory);
        var storedFileName = $"{Guid.NewGuid():N}.pdf";
        var finalPath = Path.Combine(storageDirectory, storedFileName);
        var tempPath = Path.Combine(storageDirectory, $"{Guid.NewGuid():N}.upload");
        string? previousFile = null;
        try
        {
            await using (var input = file.OpenReadStream())
            {
                var header = new byte[5];
                var read = await input.ReadAsync(header.AsMemory(0, header.Length), ct);
                if (read != header.Length || !header.AsSpan().SequenceEqual("%PDF-"u8))
                    return BadRequest(new { message = "Nội dung tệp không phải PDF hợp lệ." });
                await using var output = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
                await output.WriteAsync(header, ct);
                await input.CopyToAsync(output, ct);
            }
            var actualSize = new FileInfo(tempPath).Length;
            if (actualSize > MaxContractSize)
                return StatusCode(StatusCodes.Status413PayloadTooLarge, new { message = "Tệp hợp đồng vượt quá giới hạn 10 MiB." });
            System.IO.File.Move(tempPath, finalPath);

            await using var db = new SqlConnection(connectionString);
            await db.OpenAsync(ct);
            await using var tx = (SqlTransaction)await db.BeginTransactionAsync(ct);
            await using var lookup = db.CreateCommand();
            lookup.Transaction = tx;
            lookup.CommandText = "IF NOT EXISTS (SELECT 1 FROM dbo.Interns WITH (UPDLOCK,HOLDLOCK) WHERE Id=@intern) SELECT CAST(0 AS bit); ELSE SELECT CAST(1 AS bit);";
            lookup.Parameters.AddWithValue("@intern", internId);
            if (!Convert.ToBoolean(await lookup.ExecuteScalarAsync(ct)))
            {
                await tx.RollbackAsync(ct);
                System.IO.File.Delete(finalPath);
                return NotFound(new { message = "Không tìm thấy thực tập sinh." });
            }

            await using var existing = db.CreateCommand();
            existing.Transaction = tx;
            existing.CommandText = "SELECT StoredFileName FROM dbo.InternContracts WITH (UPDLOCK,HOLDLOCK) WHERE InternId=@intern";
            existing.Parameters.AddWithValue("@intern", internId);
            previousFile = await existing.ExecuteScalarAsync(ct) as string;

            var originalName = SafeDisplayFileName(file.FileName);
            int contractId;
            await using var save = db.CreateCommand();
            save.Transaction = tx;
            save.Parameters.AddWithValue("@intern", internId);
            save.Parameters.AddWithValue("@fileName", originalName);
            save.Parameters.AddWithValue("@stored", storedFileName);
            save.Parameters.AddWithValue("@size", actualSize);
            if (previousFile is null)
            {
                save.CommandText = "INSERT dbo.InternContracts(InternId,FileName,StoredFileName,ContentType,FileSize,UploadedAt) OUTPUT inserted.ContractId VALUES(@intern,@fileName,@stored,'application/pdf',@size,SYSUTCDATETIME())";
            }
            else
            {
                save.CommandText = "UPDATE dbo.InternContracts SET FileName=@fileName,StoredFileName=@stored,ContentType='application/pdf',FileSize=@size,UploadedAt=SYSUTCDATETIME() OUTPUT inserted.ContractId WHERE InternId=@intern";
            }
            contractId = Convert.ToInt32(await save.ExecuteScalarAsync(ct));
            await tx.CommitAsync(ct);
            if (previousFile is not null) DeleteStoredFile(previousFile);
            return Ok(new { contractId, internId, fileName = originalName, contentType = "application/pdf", fileSize = actualSize, uploadedAt = DateTimeOffset.UtcNow });
        }
        catch (SqlException)
        {
            DeleteStoredFile(storedFileName);
            return Problem("Không thể lưu thông tin hợp đồng vào cơ sở dữ liệu.", statusCode: StatusCodes.Status500InternalServerError);
        }
        catch (IOException)
        {
            DeleteStoredFile(storedFileName);
            return Problem("Không thể lưu tệp hợp đồng trên máy chủ.", statusCode: StatusCodes.Status500InternalServerError);
        }
        catch
        {
            DeleteStoredFile(storedFileName);
            throw;
        }
        finally
        {
            DeleteStoredFile(Path.GetFileName(tempPath));
        }
    }

    [HttpGet("interns/{internId:int}/contract")]
    public async Task<IActionResult> Get(int internId, CancellationToken ct)
    {
        var denied = Authorize(out var failure);
        if (denied) return failure!;
        await using var db = new SqlConnection(connectionString);
        await db.OpenAsync(ct);
        await using var command = db.CreateCommand();
        command.CommandText = "SELECT c.ContractId,c.InternId,c.FileName,c.ContentType,c.FileSize,c.UploadedAt FROM dbo.InternContracts c JOIN dbo.Interns i ON i.Id=c.InternId WHERE i.Id=@intern";
        command.Parameters.AddWithValue("@intern", internId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            await reader.CloseAsync();
            await using var exists = db.CreateCommand();
            exists.CommandText = "SELECT 1 FROM dbo.Interns WHERE Id=@intern";
            exists.Parameters.AddWithValue("@intern", internId);
            return await exists.ExecuteScalarAsync(ct) is null
                ? NotFound(new { message = "Không tìm thấy thực tập sinh." })
                : NotFound(new { message = "Thực tập sinh chưa có hợp đồng." });
        }
        return Ok(new { contractId = reader.GetInt32(0), internId = reader.GetInt32(1), fileName = reader.GetString(2), contentType = reader.GetString(3), fileSize = reader.GetInt64(4), uploadedAt = DateTime.SpecifyKind(reader.GetDateTime(5), DateTimeKind.Utc) });
    }

    [HttpGet("contracts/{contractId:int}/download")]
    public async Task<IActionResult> Download(int contractId, [FromQuery] bool inline = false, CancellationToken ct = default)
    {
        var denied = Authorize(out var failure);
        if (denied) return failure!;
        await using var db = new SqlConnection(connectionString);
        await db.OpenAsync(ct);
        await using var command = db.CreateCommand();
        command.CommandText = "SELECT FileName,StoredFileName,ContentType FROM dbo.InternContracts WHERE ContractId=@id";
        command.Parameters.AddWithValue("@id", contractId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return NotFound(new { message = "Không tìm thấy hợp đồng." });
        var fileName = reader.GetString(0);
        var stored = reader.GetString(1);
        var contentType = reader.GetString(2);
        await reader.CloseAsync();
        if (Path.GetFileName(stored) != stored) return NotFound(new { message = "Không tìm thấy tệp hợp đồng trên máy chủ." });
        var path = Path.Combine(storageDirectory, stored);
        if (!System.IO.File.Exists(path)) return NotFound(new { message = "Không tìm thấy tệp hợp đồng trên máy chủ." });
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] = "sandbox";
        Response.Headers["Content-Disposition"] = $"{(inline ? "inline" : "attachment")}; filename*=UTF-8''{Uri.EscapeDataString(fileName)}";
        return PhysicalFile(path, contentType);
    }

    private bool Authorize(out IActionResult? failure)
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            || !tokens.TryValidate(header[7..].Trim(), out var user))
        {
            failure = Unauthorized(new { message = "Bạn cần đăng nhập để thực hiện thao tác này." });
            return true;
        }
        if (!RolePermissions.HasPermission(user!.Role, "interns.manage"))
        {
            failure = StatusCode(StatusCodes.Status403Forbidden, new { message = "Bạn không có quyền quản lý hợp đồng thực tập." });
            return true;
        }
        failure = null;
        return false;
    }

    private static string SafeDisplayFileName(string? clientName)
    {
        var name = Path.GetFileName((clientName ?? "").Replace((char)92, '/'));
        var safe = new string(name.Where(c => !char.IsControl(c) && c is not '/' and not ':' and not '*' and not '?' and not '"' and not '<' and not '>' and not '|').ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(safe)) safe = "hop-dong.pdf";
        if (safe.Length > 255) safe = safe[..255];
        return safe;
    }

    private void DeleteStoredFile(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || Path.GetFileName(name) != name) return;
        try { System.IO.File.Delete(Path.Combine(storageDirectory, name)); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
