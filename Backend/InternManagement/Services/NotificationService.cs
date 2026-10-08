using InternManagement.DTOs;
using Npgsql;
using NpgsqlTypes;

namespace InternManagement.Services;

public sealed class NotificationService(IConfiguration configuration, WebPushService webPush)
{
    private readonly string connectionString = configuration.GetConnectionString("InternManagement")
        ?? throw new InvalidOperationException("Chưa cấu hình kết nối cơ sở dữ liệu.");

    public async Task<IReadOnlyList<AppNotification>> ListForUserAsync(int userId, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id,title,message,actionurl,createdat,readat
            FROM dbo.notifications WHERE userid=@user
            ORDER BY createdat DESC,id DESC LIMIT 50
            """;
        command.Parameters.Add("@user", NpgsqlDbType.Integer).Value = userId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<AppNotification>();
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new(reader.GetInt64(0), reader.GetString(1), reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetDateTime(4),
                reader.IsDBNull(5) ? null : reader.GetDateTime(5)));
        return result;
    }

    public async Task<bool> MarkReadAsync(long notificationId, int userId, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE dbo.notifications SET readat=COALESCE(readat,CURRENT_TIMESTAMP) WHERE id=@id AND userid=@user";
        command.Parameters.Add("@id", NpgsqlDbType.Bigint).Value = notificationId;
        command.Parameters.Add("@user", NpgsqlDbType.Integer).Value = userId;
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<int> ClearForUserAsync(int userId, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM dbo.notifications WHERE userid=@user";
        command.Parameters.Add("@user", NpgsqlDbType.Integer).Value = userId;
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task NotifyReviewersOfUploadAsync(int internId, string kind, CancellationToken cancellationToken)
    {
        var documentName = kind == "cv" ? "CV" : "đơn xin thực tập";
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            WITH recipients AS (
                SELECT u.id AS userid, i.fullname
                FROM dbo.users u CROSS JOIN dbo.interns i
                WHERE u.role IN ('HR','ADMIN') AND i.id=@intern
            ), inserted AS (
                INSERT INTO dbo.notifications(userid,title,message,actionurl)
                SELECT userid,'Có tài liệu thực tập mới',fullname || ' vừa nộp ' || @document || '. Vui lòng mở danh sách duyệt tài liệu.',
                    'document-reviews.html' FROM recipients
                RETURNING userid,title,message,actionurl
            ) SELECT userid,title,message,actionurl FROM inserted
            """;
        command.Parameters.Add("@document", NpgsqlDbType.Varchar, 100).Value = documentName;
        command.Parameters.Add("@intern", NpgsqlDbType.Integer).Value = internId;
        var recipients = new List<(int UserId, string Title, string Message, string ActionUrl)>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken))
                recipients.Add((reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
        foreach (var recipient in recipients)
            await webPush.SendToUserAsync(recipient.UserId, recipient.Title, recipient.Message, recipient.ActionUrl, cancellationToken);
    }

    public async Task<bool> NotifyInternAsync(string email, string title, string message, string actionUrl, CancellationToken cancellationToken)
    {
        if (message.Length > 1000) message = message[..1000];
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            WITH inserted AS (
                INSERT INTO dbo.notifications(userid,title,message,actionurl)
                SELECT u.id,@title,@message,@actionUrl FROM dbo.users u
                JOIN dbo.interns i ON LOWER(i.email)=LOWER(u.email)
                WHERE LOWER(i.email)=LOWER(@email) AND u.role='INTERN'
                RETURNING userid
            ) SELECT userid FROM inserted
            """;
        command.Parameters.Add("@email", NpgsqlDbType.Varchar, 254).Value = email;
        command.Parameters.Add("@title", NpgsqlDbType.Varchar, 150).Value = title;
        command.Parameters.Add("@message", NpgsqlDbType.Varchar, 1000).Value = message;
        command.Parameters.Add("@actionUrl", NpgsqlDbType.Varchar, 300).Value = actionUrl;
        var recipients = new List<int>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken)) recipients.Add(reader.GetInt32(0));
        foreach (var userId in recipients)
            await webPush.SendToUserAsync(userId, title, message, actionUrl, cancellationToken);
        return recipients.Count > 0;
    }
}
