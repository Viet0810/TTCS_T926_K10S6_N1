using System.Data;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using NpgsqlTypes;

namespace InternManagement.Services;

public sealed class PasswordResetService(
    IConfiguration configuration,
    PasswordHasher passwords,
    ILogger<PasswordResetService> logger)
{
    private readonly string connectionString = configuration.GetConnectionString("InternManagement")
        ?? throw new InvalidOperationException("Chưa cấu hình kết nối cơ sở dữ liệu.");

    public async Task RequestAsync(string email, CancellationToken cancellationToken)
    {
        // Validate delivery configuration before looking up an account.
        var smtp = configuration.GetSection("Smtp");
        var host = smtp["Host"];
        var sender = smtp["FromAddress"];
        var resetPage = configuration["PasswordReset:ResetPageUrl"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(sender)
            || !Uri.TryCreate(resetPage, UriKind.Absolute, out var page)
            || (page.Scheme != Uri.UriSchemeHttps && !(page.IsLoopback && page.Scheme == Uri.UriSchemeHttp)))
            throw new PasswordRecoveryUnavailableException("Chưa cấu hình dịch vụ gửi email đặt lại mật khẩu.");

        var from = new MailAddress(sender, "Hệ thống quản lý thực tập sinh", Encoding.UTF8);
        var port = smtp.GetValue("Port", 587);
        if (port is < 1 or > 65535)
            throw new PasswordRecoveryUnavailableException("Cổng SMTP không hợp lệ.");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var lookup = connection.CreateCommand();
        lookup.CommandText = "SELECT Id FROM dbo.Users WHERE Email = @email";
        lookup.Parameters.Add("@email", NpgsqlDbType.Varchar, 254).Value = email.Trim();
        var result = await lookup.ExecuteScalarAsync(cancellationToken);
        if (result is not int userId) return;

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var tokenHash = HashToken(token);
        var lifetime = Math.Clamp(configuration.GetValue("PasswordReset:TokenLifetimeMinutes", 30), 5, 60);
        await using var create = connection.CreateCommand();
        create.CommandText = """
            INSERT INTO dbo.PasswordResetTokens (UserId, TokenHash, ExpiresAt)
            SELECT @userId, @tokenHash, CURRENT_TIMESTAMP + (@lifetime * INTERVAL '1 minute')
            WHERE NOT EXISTS (
                SELECT 1 FROM dbo.PasswordResetTokens
                WHERE UserId = @userId AND CreatedAt > CURRENT_TIMESTAMP - INTERVAL '1 minute'
            );
            """;
        create.Parameters.Add("@userId", NpgsqlDbType.Integer).Value = userId;
        create.Parameters.Add("@tokenHash", NpgsqlDbType.Bytea, 32).Value = tokenHash;
        create.Parameters.Add("@lifetime", NpgsqlDbType.Integer).Value = lifetime;
        if (await create.ExecuteNonQueryAsync(cancellationToken) == 0) return;

        // A fragment keeps the secret out of HTTP request paths and Referer headers.
        var link = new UriBuilder(page) { Fragment = "token=" + token }.Uri.AbsoluteUri;
        using var message = new MailMessage
        {
            From = from,
            Subject = "Đặt lại mật khẩu | Hệ thống quản lý thực tập sinh",
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
            Body = $"Bạn đã yêu cầu đặt lại mật khẩu.\n\nMở liên kết sau để tạo mật khẩu mới:\n{link}\n\n"
                + $"Liên kết có hiệu lực trong {lifetime} phút và chỉ dùng được một lần.\n"
                + "Nếu bạn không gửi yêu cầu này, hãy bỏ qua email."
        };
        message.To.Add(new MailAddress(email.Trim()));
        using var client = new SmtpClient(host, port)
        {
            EnableSsl = smtp.GetValue("EnableSsl", true),
            UseDefaultCredentials = false,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 15000
        };
        if (!string.IsNullOrWhiteSpace(smtp["Username"]))
            client.Credentials = new NetworkCredential(smtp["Username"], smtp["Password"]);

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            await client.SendMailAsync(message, timeout.Token);
        }
        catch
        {
            // A failed delivery must never leave a usable reset link in storage.
            await using var remove = connection.CreateCommand();
            remove.CommandText = "DELETE FROM dbo.PasswordResetTokens WHERE TokenHash = @tokenHash";
            remove.Parameters.Add("@tokenHash", NpgsqlDbType.Bytea, 32).Value = tokenHash;
            await remove.ExecuteNonQueryAsync(CancellationToken.None);
            logger.LogWarning("Không gửi được email đặt lại mật khẩu qua SMTP.");
            throw new PasswordRecoveryUnavailableException("Dịch vụ email hiện không khả dụng. Vui lòng thử lại sau hoặc liên hệ quản trị viên.");
        }
    }

    public async Task<bool> ResetAsync(string token, string password, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (NpgsqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using var claim = connection.CreateCommand();
        claim.Transaction = transaction;
        claim.CommandText = """
            UPDATE dbo.PasswordResetTokens
            SET UsedAt = CURRENT_TIMESTAMP
            WHERE TokenHash = @tokenHash AND UsedAt IS NULL AND ExpiresAt > CURRENT_TIMESTAMP
            RETURNING UserId;
            """;
        claim.Parameters.Add("@tokenHash", NpgsqlDbType.Bytea, 32).Value = HashToken(token);
        var result = await claim.ExecuteScalarAsync(cancellationToken);
        if (result is not int userId)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await using var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = """
            UPDATE dbo.Users SET PasswordHash = @passwordHash WHERE Id = @userId;
            """;
        update.Parameters.Add("@userId", NpgsqlDbType.Integer).Value = userId;
        update.Parameters.Add("@passwordHash", NpgsqlDbType.Varchar, 512).Value = passwords.Hash(password);
        await update.ExecuteNonQueryAsync(cancellationToken);
        await using var invalidate = connection.CreateCommand();
        invalidate.Transaction = transaction;
        invalidate.CommandText = "UPDATE dbo.PasswordResetTokens SET UsedAt = CURRENT_TIMESTAMP WHERE UserId = @userId AND UsedAt IS NULL";
        invalidate.Parameters.Add("@userId", NpgsqlDbType.Integer).Value = userId;
        await invalidate.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static byte[] HashToken(string token) => SHA256.HashData(Convert.FromHexString(token));
}
