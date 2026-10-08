using System.Buffers.Binary;
using InternManagement.DTOs;
using Npgsql;
using NpgsqlTypes;

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
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT i.Id FROM dbo.Interns i JOIN dbo.Users u ON LOWER(i.Email)=LOWER(u.Email) WHERE u.Id=@user";
        command.Parameters.Add("@user", NpgsqlDbType.Integer).Value = userId;
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is int id ? id : null;
    }

    public async Task<IReadOnlyList<InternDocumentResponse>> ListOwnedAsync(int ownerId, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Kind,FileName,octet_length(Content),UploadedAt,ReviewStatus,ReviewComment
            FROM dbo.InternDocuments WHERE InternId=@id ORDER BY Kind
            """;
        command.Parameters.Add("@id", NpgsqlDbType.Integer).Value = ownerId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var documents = new List<InternDocumentResponse>();
        while (await reader.ReadAsync(cancellationToken))
            documents.Add(new(reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetDateTime(3),
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

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        // Serialize replacement by owner/kind; a failed save retains the previous content and review.
        command.CommandText = """
            INSERT INTO dbo.InternDocuments(InternId,Kind,FileName,Content)
            VALUES(@id,@kind,@name,@content)
            ON CONFLICT (InternId,Kind) DO UPDATE SET FileName=EXCLUDED.FileName,
                Content=EXCLUDED.Content,UploadedAt=CURRENT_TIMESTAMP,ReviewStatus='pending',
                ReviewComment=NULL,ReviewedAt=NULL,ReviewedBy=NULL,NotificationStatus='not_sent',
                NotificationMessage=NULL,NotificationAttemptedAt=NULL,Version=interndocuments.Version+1;
            """;
        AddDocumentKey(command, ownerId, kind);
        command.Parameters.Add("@name", NpgsqlDbType.Varchar, 255).Value = name;
        command.Parameters.Add("@content", NpgsqlDbType.Bytea, -1).Value = bytes;
        await command.ExecuteNonQueryAsync(cancellationToken);
        return null;
    }

    private static string? ValidateUpload(string kind, IFormFile? file)
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
        await using var connection = new NpgsqlConnection(connectionString);
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
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT d.InternId,i.FullName,i.StudentCode,i.Email,d.Kind,d.FileName,octet_length(d.Content),d.UploadedAt,
                d.ReviewStatus,d.ReviewComment,d.ReviewedAt,u.FullName,d.Version,
                d.NotificationStatus,d.NotificationMessage,d.NotificationAttemptedAt
            FROM dbo.InternDocuments d JOIN dbo.Interns i ON i.Id=d.InternId
            LEFT JOIN dbo.Users u ON u.Id=d.ReviewedBy ORDER BY d.UploadedAt DESC
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var documents = new List<DocumentReviewResponse>();
        while (await reader.ReadAsync(cancellationToken)) documents.Add(MapReview(reader));
        return documents;
    }

    public async Task<bool> SaveNotificationResultAsync(int ownerId, string kind, string reviewStatus,
        ReviewNotificationResult result, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE dbo.InternDocuments
            SET NotificationStatus=@notificationStatus, NotificationMessage=@message,
                NotificationAttemptedAt=@attemptedAt
            WHERE InternId=@id AND Kind=@kind AND ReviewStatus=@reviewStatus
            """;
        AddDocumentKey(command, ownerId, kind);
        command.Parameters.Add("@reviewStatus", NpgsqlDbType.Varchar, 20).Value = reviewStatus;
        command.Parameters.Add("@notificationStatus", NpgsqlDbType.Varchar, 20).Value = result.Status;
        command.Parameters.Add("@message", NpgsqlDbType.Varchar, 500).Value = result.Message;
        command.Parameters.Add("@attemptedAt", NpgsqlDbType.TimestampTz).Value = result.AttemptedAt;
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<DocumentReviewResult> ReviewAsync(int ownerId, string kind, int userId,
        ReviewDocumentRequest request, byte[] version, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        // A changed upload or decision must not be overwritten by an old review dialog.
        command.CommandText = """
            UPDATE dbo.InternDocuments SET ReviewStatus=@status,ReviewComment=@comment,ReviewedAt=CURRENT_TIMESTAMP,ReviewedBy=@user,Version=Version+1
            WHERE InternId=@id AND Kind=@kind AND Version=@version AND ReviewStatus<>@status
            """;
        AddDocumentKey(command, ownerId, kind);
        command.Parameters.Add("@status", NpgsqlDbType.Varchar, 20).Value = request.Status;
        command.Parameters.Add("@comment", NpgsqlDbType.Varchar, 2000).Value = (object?)request.Comment?.Trim() ?? DBNull.Value;
        command.Parameters.Add("@user", NpgsqlDbType.Integer).Value = userId;
        command.Parameters.Add("@version", NpgsqlDbType.Bigint).Value = BinaryPrimitives.ReadInt64BigEndian(version);
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 1) return DocumentReviewResult.Updated;

        command.CommandText = "SELECT ReviewStatus FROM dbo.InternDocuments WHERE InternId=@id AND Kind=@kind";
        var current = await command.ExecuteScalarAsync(cancellationToken);
        if (current is null) return DocumentReviewResult.NotFound;
        return (string)current == request.Status ? DocumentReviewResult.AlreadyReviewed : DocumentReviewResult.Changed;
    }

    private static void AddDocumentKey(NpgsqlCommand command, int ownerId, string kind)
    {
        command.Parameters.Add("@id", NpgsqlDbType.Integer).Value = ownerId;
        command.Parameters.Add("@kind", NpgsqlDbType.Varchar, 20).Value = kind;
    }

    private static DocumentReviewResponse MapReview(NpgsqlDataReader reader) => new(
        reader.GetInt32(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2),
        reader.GetString(3), reader.GetString(4), reader.GetString(5), Convert.ToInt64(reader.GetValue(6)), reader.GetDateTime(7),
        reader.GetString(8), reader.IsDBNull(9) ? null : reader.GetString(9),
        reader.IsDBNull(10) ? null : reader.GetDateTime(10), reader.IsDBNull(11) ? null : reader.GetString(11),
        Convert.ToBase64String(ToVersionBytes(reader.GetInt64(12))), reader.GetString(13),
        reader.IsDBNull(14) ? null : reader.GetString(14), reader.IsDBNull(15) ? null : reader.GetDateTime(15));

    private static byte[] ToVersionBytes(long version)
    {
        var bytes = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, version);
        return bytes;
    }
}
