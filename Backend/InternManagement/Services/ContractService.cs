using System.Data;
using InternManagement.DTOs;
using Microsoft.Data.SqlClient;
namespace InternManagement.Services;

public sealed class ContractService(IConfiguration config)
{
    private readonly string connectionString = config.GetConnectionString("InternManagement")!;
    public async Task<List<ContractResponse>> ListAsync(int? owner, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString); await connection.OpenAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT c.InternId,i.FullName,i.Email,c.FileName,DATALENGTH(c.Content),c.UploadedAt,c.ConfirmedAt,c.Version
            FROM dbo.InternContracts c JOIN dbo.Interns i ON i.Id=c.InternId
            WHERE @Owner IS NULL OR c.InternId=@Owner ORDER BY c.UploadedAt DESC
            """;
        cmd.Parameters.Add("@Owner", SqlDbType.Int).Value = (object?)owner ?? DBNull.Value;
        await using var reader = await cmd.ExecuteReaderAsync(ct); var result = new List<ContractResponse>();
        while (await reader.ReadAsync(ct)) result.Add(new(reader.GetInt32(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.GetInt64(4),reader.GetDateTime(5),reader.IsDBNull(6)?null:reader.GetDateTime(6),Convert.ToBase64String((byte[])reader[7])));
        return result;
    }
    public async Task<string?> UploadAsync(int internId, int userId, IFormFile? file, CancellationToken ct)
    {
        var error = InternDocumentService.ValidateUpload("cv", file); if (error is not null) return error;
        using var buffer = new MemoryStream(); await file!.CopyToAsync(buffer,ct); var bytes=buffer.ToArray();
        if (bytes.Length < 5 || !bytes.AsSpan(0,5).SequenceEqual("%PDF-"u8)) return "Nội dung tệp không phải PDF.";
        await using var connection=new SqlConnection(connectionString); await connection.OpenAsync(ct);
        await using var cmd=connection.CreateCommand();
        cmd.CommandText="""
            SET XACT_ABORT ON; BEGIN TRANSACTION;
            IF NOT EXISTS(SELECT 1 FROM dbo.Interns WITH(UPDLOCK,HOLDLOCK) WHERE Id=@Id)
            BEGIN ROLLBACK; SELECT 1; RETURN; END;
            IF EXISTS(SELECT 1 FROM dbo.InternContracts WITH(UPDLOCK,HOLDLOCK) WHERE InternId=@Id AND ConfirmedAt IS NOT NULL)
            BEGIN ROLLBACK; SELECT 2; RETURN; END;
            UPDATE dbo.InternContracts SET FileName=@Name,Content=@Content,UploadedAt=SYSUTCDATETIME(),UploadedBy=@User WHERE InternId=@Id;
            IF @@ROWCOUNT=0 INSERT dbo.InternContracts(InternId,FileName,Content,UploadedBy) VALUES(@Id,@Name,@Content,@User);
            COMMIT; SELECT 0;
            """;
        cmd.Parameters.Add("@Id",SqlDbType.Int).Value=internId; cmd.Parameters.Add("@User",SqlDbType.Int).Value=userId;
        cmd.Parameters.Add("@Name",SqlDbType.NVarChar,255).Value=Path.GetFileName(file.FileName.Replace('\\','/'));
        cmd.Parameters.Add("@Content",SqlDbType.VarBinary,-1).Value=bytes;
        var result=Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        return result==1?"Không tìm thấy thực tập sinh.":result==2?"Hợp đồng đã xác nhận, không thể thay thế.":null;
    }
    public async Task<StoredDocument?> DownloadAsync(int internId,CancellationToken ct)
    {
        await using var connection=new SqlConnection(connectionString); await connection.OpenAsync(ct);
        await using var cmd=connection.CreateCommand(); cmd.CommandText="SELECT FileName,Content FROM dbo.InternContracts WHERE InternId=@Id";
        cmd.Parameters.Add("@Id",SqlDbType.Int).Value=internId;
        await using var reader=await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)?new StoredDocument(reader.GetString(0),(byte[])reader[1]):null;
    }
    public async Task<bool> ConfirmAsync(int internId,byte[] version,CancellationToken ct)
    {
        await using var connection=new SqlConnection(connectionString); await connection.OpenAsync(ct);
        await using var cmd=connection.CreateCommand();
        cmd.CommandText="UPDATE dbo.InternContracts SET ConfirmedAt=SYSUTCDATETIME() WHERE InternId=@Id AND ConfirmedAt IS NULL AND Version=@Version";
        cmd.Parameters.Add("@Id",SqlDbType.Int).Value=internId; cmd.Parameters.Add("@Version",SqlDbType.Binary,8).Value=version;
        return await cmd.ExecuteNonQueryAsync(ct)==1;
    }
}
