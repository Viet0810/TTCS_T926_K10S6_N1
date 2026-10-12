using System.Data;
using InternManagement.DTOs;
using Microsoft.Data.SqlClient;

namespace InternManagement.Services;

public enum DocumentReviewResult { Updated, NotFound, AlreadyReviewed, Changed }

public sealed class InternDocumentService(IConfiguration configuration)
{
    public const int MaxBytes = 5 * 1024 * 1024;
    private readonly string connectionString = configuration.GetConnectionString("InternManagement")
        ?? throw new InvalidOperationException("Chưa cấu hình kết nối cơ sở dữ liệu.");

    public static bool ValidKind(string kind) => kind is "cv" or "application";

    public async Task<int?> GetOwnerIdAsync(int userId, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT i.Id FROM dbo.Interns i JOIN dbo.Users u ON i.Email=u.Email WHERE u.Id=@user";
        command.Parameters.Add("@user", SqlDbType.Int).Value = userId;
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is int id ? id : null;
    }

    public async Task<IReadOnlyList<InternDocumentResponse>> ListOwnedAsync(int ownerId, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Kind,FileName,DATALENGTH(Content),UploadedAt,ReviewStatus,ReviewComment
            FROM dbo.InternDocuments WHERE InternId=@id ORDER BY Kind
            """;
        command.Parameters.Add("@id", SqlDbType.Int).Value = ownerId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var documents = new List<InternDocumentResponse>();
        while (await reader.ReadAsync(cancellationToken))
            documents.Add(new(reader.GetString(0), reader.GetString(1), reader.GetInt64(2), reader.GetDateTime(3),
                reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5)));
        return documents;
    }

    public async Task<string?> UploadAsync(int ownerId, string kind, IFormFile? file, CancellationToken cancellationToken)
    {
        var validation = ValidateUpload(kind, file);
        if (validation is not null) return validation;
        var name = Path.GetFileName(file!.FileName.Replace('\\', '/'));
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        if (bytes.Length < 5 || !bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
            return "Nội dung tệp không phải tài liệu PDF.";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        // Serialize replacement by owner/kind; a failed save retains the previous content and review.
        command.CommandText = """
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;
            UPDATE dbo.InternDocuments WITH (UPDLOCK,SERIALIZABLE) SET FileName=@name,Content=@content,UploadedAt=SYSUTCDATETIME(),ReviewStatus='pending',ReviewComment=NULL,ReviewedAt=NULL,ReviewedBy=NULL WHERE InternId=@id AND Kind=@kind;
            IF @@ROWCOUNT=0 INSERT INTO dbo.InternDocuments(InternId,Kind,FileName,Content) VALUES(@id,@kind,@name,@content);
            COMMIT TRANSACTION;
            """;
        AddDocumentKey(command, ownerId, kind);
        command.Parameters.Add("@name", SqlDbType.NVarChar, 255).Value = name;
        command.Parameters.Add("@content", SqlDbType.VarBinary, -1).Value = bytes;
        await command.ExecuteNonQueryAsync(cancellationToken);
        return null;
    }

    internal static string? ValidateUpload(string kind, IFormFile? file)
    {
        if (!ValidKind(kind)) return "Loại tài liệu không hợp lệ.";
        if (file is null) return "Vui lòng chọn tài liệu cần tải lên.";
        if (file.Length == 0) return "Tệp đã chọn rỗng. Vui lòng chọn tài liệu có nội dung.";
        if (file.Length > MaxBytes) return "Tệp vượt quá dung lượng cho phép. Vui lòng chọn PDF tối đa 5 MB.";
        var name = Path.GetFileName(file.FileName.Replace('\\', '/'));
        if (name.Length > 255 || !name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) || name.Any(char.IsControl))
            return "Tên tệp phải là PDF, tối đa 255 ký tự.";
        // Generic or absent MIME still requires extension and signature checks.
        if (!string.IsNullOrEmpty(file.ContentType)
            && !file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
            && !file.ContentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
            return "Định dạng tệp không hợp lệ. Chỉ hỗ trợ tài liệu PDF.";
        return null;
    }

    public async Task<StoredDocument?> DownloadAsync(int ownerId, string kind, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT FileName,Content FROM dbo.InternDocuments WHERE InternId=@id AND Kind=@kind";
        AddDocumentKey(command, ownerId, kind);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new StoredDocument(reader.GetString(0), (byte[])reader[1]) : null;
    }

    public async Task<IReadOnlyList<DocumentReviewResponse>> ListReviewsAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT d.InternId,i.FullName,i.StudentCode,i.Email,d.Kind,d.FileName,DATALENGTH(d.Content),d.UploadedAt,
                d.ReviewStatus,d.ReviewComment,d.ReviewedAt,u.FullName,d.Version,
                i.Phone,i.School,i.Major,i.CreatedAt
            FROM dbo.InternDocuments d JOIN dbo.Interns i ON i.Id=d.InternId
            LEFT JOIN dbo.Users u ON u.Id=d.ReviewedBy ORDER BY d.UploadedAt DESC
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var documents = new List<DocumentReviewResponse>();
        while (await reader.ReadAsync(cancellationToken)) documents.Add(MapReview(reader));
        return documents;
    }

    public async Task<DocumentReviewResult> ReviewAsync(int ownerId, string kind, int userId,
        ReviewDocumentRequest request, byte[] version, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        // A changed upload or decision must not be overwritten by an old review dialog.
        command.CommandText = """
            UPDATE dbo.InternDocuments SET ReviewStatus=@status,ReviewComment=@comment,ReviewedAt=SYSUTCDATETIME(),ReviewedBy=@user
            WHERE InternId=@id AND Kind=@kind AND Version=@version AND ReviewStatus='pending'
            """;
        AddDocumentKey(command, ownerId, kind);
        command.Parameters.Add("@status", SqlDbType.VarChar, 20).Value = request.Status;
        command.Parameters.Add("@comment", SqlDbType.NVarChar, 2000).Value = (object?)request.Comment?.Trim() ?? DBNull.Value;
        command.Parameters.Add("@user", SqlDbType.Int).Value = userId;
        command.Parameters.Add("@version", SqlDbType.Binary, 8).Value = version;
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 1) return DocumentReviewResult.Updated;

        command.CommandText = "SELECT ReviewStatus FROM dbo.InternDocuments WHERE InternId=@id AND Kind=@kind";
        var current = await command.ExecuteScalarAsync(cancellationToken);
        if (current is null) return DocumentReviewResult.NotFound;
        return (string)current != "pending" ? DocumentReviewResult.AlreadyReviewed : DocumentReviewResult.Changed;
    }

    private static void AddDocumentKey(SqlCommand command, int ownerId, string kind)
    {
        command.Parameters.Add("@id", SqlDbType.Int).Value = ownerId;
        command.Parameters.Add("@kind", SqlDbType.VarChar, 20).Value = kind;
    }

    private static DocumentReviewResponse MapReview(SqlDataReader reader) => new(
        reader.GetInt32(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2),
        reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetInt64(6), reader.GetDateTime(7),
        reader.GetString(8), reader.IsDBNull(9) ? null : reader.GetString(9),
        reader.IsDBNull(10) ? null : reader.GetDateTime(10), reader.IsDBNull(11) ? null : reader.GetString(11),
        Convert.ToBase64String((byte[])reader[12]), reader.GetString(13), reader.GetString(14),
        reader.GetString(15), reader.GetFieldValue<DateTimeOffset>(16));
}
