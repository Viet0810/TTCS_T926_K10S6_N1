using System.Data;
using InternManagement.DTOs;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace InternManagement.Services;

public sealed class PasswordResetService(
    IConfiguration configuration,
    PasswordHasher passwords,
    ILogger<PasswordResetService> logger)
{
    private readonly string connectionString = configuration.GetConnectionString("InternManagement")
        ?? throw new InvalidOperationException("Chưa cấu hình kết nối cơ sở dữ liệu.");

    public Task RequestAsync(string email, CancellationToken cancellationToken)
        => RequestCoreAsync(email, cancellationToken);

    public async Task SendReviewResultAsync(string recipient, string fullName, string kind, string status, string? reason, CancellationToken cancellationToken)
    {
        if (status is not ("approved" or "rejected")) throw new ArgumentException("Kết quả xét duyệt không hợp lệ.");
        var approved = status == "approved";
        using var message = new MailMessage
        {
            Subject = "Kết quả xét duyệt hồ sơ | Hệ thống quản lý thực tập sinh",
            SubjectEncoding = Encoding.UTF8, BodyEncoding = Encoding.UTF8,
            Body = $"Xin chào {fullName},\n\n"
                + $"{(kind == "cv" ? "CV" : "Đơn xin thực tập")} của bạn {(approved ? "đã được chấp nhận" : "chưa được chấp nhận")}.\n"
                + $"Trạng thái: {(approved ? "Approved" : "Rejected")}\n"
                + (!approved && !string.IsNullOrWhiteSpace(reason) ? $"Lý do: {reason.Trim()}\n" : "")
                + "\nTrân trọng,\nHệ thống quản lý thực tập sinh"
        };
        message.To.Add(new MailAddress(recipient));
        await SendMailAsync(message, cancellationToken);
    }

    public async Task<bool> SendAccountCreatedAsync(UserResponse user, string temporaryPassword, CancellationToken cancellationToken)
    {
        if (user.Role is not ("HR" or "MENTOR")) return false;
        var loginUrl = configuration["Frontend:LoginUrl"];
        if (string.IsNullOrWhiteSpace(loginUrl) && Uri.TryCreate(configuration["PasswordReset:ResetPageUrl"], UriKind.Absolute, out var resetPage))
            loginUrl = new Uri(resetPage, "index.html").AbsoluteUri;
        var loginLink = Uri.TryCreate(loginUrl, UriKind.Absolute, out var page)
            && (page.Scheme == Uri.UriSchemeHttps || (page.IsLoopback && page.Scheme == Uri.UriSchemeHttp))
            ? $"\nĐăng nhập: {page.AbsoluteUri}\n" : "";
        using var message = new MailMessage
        {
            Subject = "Tài khoản Hệ thống quản lý thực tập sinh",
            SubjectEncoding = Encoding.UTF8, BodyEncoding = Encoding.UTF8,
            Body = $"Xin chào {user.FullName},\n\nTài khoản của bạn đã được tạo.\n\n"
                + $"Email đăng nhập: {user.Email}\nMật khẩu tạm thời: {temporaryPassword}\n"
                + $"Vai trò: {(user.Role == "MENTOR" ? "Mentor" : "HR")}\n\n"
                + "Vui lòng đăng nhập và đổi mật khẩu sau lần đăng nhập đầu tiên.\n"
                + loginLink + "\nTrân trọng,\nHệ thống quản lý thực tập sinh"
        };
        message.To.Add(new MailAddress(user.Email));
        await SendMailAsync(message, cancellationToken);
        return true;
    }

    private async Task<bool> RequestCoreAsync(string email, CancellationToken cancellationToken)
    {
        // The reset page is separate from SMTP; report its configuration error accurately.
        var resetPage = configuration["PasswordReset:ResetPageUrl"];
        if (!Uri.TryCreate(resetPage, UriKind.Absolute, out var page)
            || (page.Scheme != Uri.UriSchemeHttps && !(page.IsLoopback && page.Scheme == Uri.UriSchemeHttp)))
        {
            logger.LogWarning("Password recovery failed. Stage ResetUrlConfiguration, exception type {FailureType}, message {FailureMessage}.",
                nameof(PasswordRecoveryUnavailableException), "PasswordReset:ResetPageUrl is missing or invalid.");
            throw new PasswordRecoveryUnavailableException("Chưa cấu hình hoặc cấu hình không hợp lệ địa chỉ trang đặt lại mật khẩu (PasswordReset:ResetPageUrl). Vui lòng liên hệ quản trị viên.");
        }
        ReadSmtpSettings();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var lookup = connection.CreateCommand();
        lookup.CommandText = "SELECT Id FROM dbo.Users WHERE Email = @email";
        lookup.Parameters.Add("@email", SqlDbType.NVarChar, 254).Value = email.Trim();
        var result = await lookup.ExecuteScalarAsync(cancellationToken);
        if (result is not int userId) return false;

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var tokenHash = HashToken(token);
        var lifetime = Math.Clamp(configuration.GetValue("PasswordReset:TokenLifetimeMinutes", 30), 5, 60);
        await using var create = connection.CreateCommand();
        create.CommandText = """
            INSERT INTO dbo.PasswordResetTokens (UserId, TokenHash, ExpiresAt)
            SELECT @userId, @tokenHash, DATEADD(MINUTE, @lifetime, SYSUTCDATETIME())
            WHERE NOT EXISTS (
                SELECT 1 FROM dbo.PasswordResetTokens WITH (UPDLOCK, HOLDLOCK)
                WHERE UserId = @userId AND CreatedAt > DATEADD(MINUTE, -1, SYSUTCDATETIME())
            );
            """;
        create.Parameters.Add("@userId", SqlDbType.Int).Value = userId;
        create.Parameters.Add("@tokenHash", SqlDbType.Binary, 32).Value = tokenHash;
        create.Parameters.Add("@lifetime", SqlDbType.Int).Value = lifetime;
        if (await create.ExecuteNonQueryAsync(cancellationToken) == 0) return false;

        // A fragment keeps the secret out of HTTP request paths and Referer headers.
        var link = new UriBuilder(page) { Fragment = "token=" + token }.Uri.AbsoluteUri;
        using var message = new MailMessage
        {
            Subject = "Đặt lại mật khẩu | Hệ thống quản lý thực tập sinh",
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
            Body = $"Bạn đã yêu cầu đặt lại mật khẩu.\n\nMở liên kết sau để tạo mật khẩu mới:\n{link}\n\n"
                + $"Liên kết có hiệu lực trong {lifetime} phút và chỉ dùng được một lần.\n"
                + "Nếu bạn không gửi yêu cầu này, hãy bỏ qua email."
        };
        message.To.Add(new MailAddress(email.Trim()));
        try
        {
            await SendMailAsync(message, cancellationToken);
            return true;
        }
        catch (Exception error)
        {
            // A failed delivery must never leave a usable reset link in storage.
            await using var remove = connection.CreateCommand();
            remove.CommandText = "DELETE FROM dbo.PasswordResetTokens WHERE TokenHash = @tokenHash";
            remove.Parameters.Add("@tokenHash", SqlDbType.Binary, 32).Value = tokenHash;
            await remove.ExecuteNonQueryAsync(CancellationToken.None);
            if (error is PasswordRecoveryUnavailableException) throw;
            throw new PasswordRecoveryUnavailableException("Dịch vụ email hiện không khả dụng. Vui lòng thử lại sau hoặc liên hệ quản trị viên.");
        }
    }

    public async Task<bool> ResetAsync(string token, string password, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using var claim = connection.CreateCommand();
        claim.Transaction = transaction;
        claim.CommandText = """
            UPDATE dbo.PasswordResetTokens WITH (UPDLOCK)
            SET UsedAt = SYSUTCDATETIME()
            OUTPUT INSERTED.UserId
            WHERE TokenHash = @tokenHash AND UsedAt IS NULL AND ExpiresAt > SYSUTCDATETIME();
            """;
        claim.Parameters.Add("@tokenHash", SqlDbType.Binary, 32).Value = HashToken(token);
        var result = await claim.ExecuteScalarAsync(cancellationToken);
        if (result is not int userId)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await using var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = """
            UPDATE dbo.Users SET PasswordHash = @passwordHash, MustChangePassword = 0 WHERE Id = @userId;
            UPDATE dbo.PasswordResetTokens SET UsedAt = SYSUTCDATETIME()
            WHERE UserId = @userId AND UsedAt IS NULL;
            """;
        update.Parameters.Add("@userId", SqlDbType.Int).Value = userId;
        update.Parameters.Add("@passwordHash", SqlDbType.NVarChar, 512).Value = passwords.Hash(password);
        await update.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static byte[] HashToken(string token) => SHA256.HashData(Convert.FromHexString(token));

    private (string Host, int Port, bool EnableSsl, string Sender) ReadSmtpSettings()
    {
        var smtp = configuration.GetSection("Smtp");
        try
        {
            var host = smtp["Host"]; var sender = smtp["FromAddress"];
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(sender))
                throw new PasswordRecoveryUnavailableException("Chưa cấu hình SMTP Host hoặc địa chỉ email gửi.");
            var port = smtp.GetValue("Port", 587);
            var ssl = smtp.GetValue("EnableSsl", true);
            if (port is < 1 or > 65535) throw new PasswordRecoveryUnavailableException("Cổng SMTP không hợp lệ.");
            if (host.Equals("smtp.gmail.com", StringComparison.OrdinalIgnoreCase)
                && (port != 587 || !ssl || string.IsNullOrWhiteSpace(smtp["Username"]) || string.IsNullOrWhiteSpace(smtp["Password"])))
                throw new PasswordRecoveryUnavailableException("Gmail SMTP cần port 587, SSL, tài khoản gửi và App Password.");
            return (host, port, ssl, sender);
        }
        catch (Exception error)
        {
            logger.LogWarning("Email configuration failed. Stage Configuration, exception type {FailureType}, message {FailureMessage}.",
                error.GetType().Name, RedactMailError(error.Message, smtp["Password"], ""));
            if (error is PasswordRecoveryUnavailableException) throw;
            throw new PasswordRecoveryUnavailableException("Cấu hình SMTP không hợp lệ. Vui lòng liên hệ quản trị viên.");
        }
    }

    private async Task SendMailAsync(MailMessage message, CancellationToken cancellationToken)
    {
        var smtp = configuration.GetSection("Smtp");
        var stage = "Configuration";
        try
        {
            var settings = ReadSmtpSettings();
            stage = "MessageConstruction";
            message.From = new MailAddress(settings.Sender, "Hệ thống quản lý thực tập sinh", Encoding.UTF8);
            using var client = new SmtpClient(settings.Host, settings.Port)
            {
                EnableSsl = settings.EnableSsl, UseDefaultCredentials = false,
                DeliveryMethod = SmtpDeliveryMethod.Network, Timeout = 15000
            };
            if (!string.IsNullOrWhiteSpace(smtp["Username"])) client.Credentials = new NetworkCredential(smtp["Username"], smtp["Password"]);
            stage = "SMTP.SendMailAsync";
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            await client.SendMailAsync(message, timeout.Token);
            logger.LogInformation("Email sent to {Recipient}.", message.To[0].Address);
        }
        catch (Exception error)
        {
            var details = error.Message + (error.InnerException is null ? "" : " | " + error.InnerException.Message);
            logger.LogWarning("Email send failed to {Recipient}. Stage {Stage}, exception type {FailureType}, exception message {FailureMessage}, SMTP status {SmtpStatus}.",
                message.To[0].Address, stage, error.GetType().Name, RedactMailError(details, smtp["Password"], message.Body),
                error is SmtpException smtpError ? smtpError.StatusCode.ToString() : "Unavailable");
            if (error is PasswordRecoveryUnavailableException) throw;
            throw new PasswordRecoveryUnavailableException("Dịch vụ email hiện không khả dụng. Vui lòng kiểm tra cấu hình SMTP hoặc thử lại sau.");
        }
    }

    internal static string RedactMailError(string details, string? smtpPassword, string body)
    {
        var secrets = new List<string>();
        if (!string.IsNullOrEmpty(smtpPassword)) secrets.Add(smtpPassword);
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(body,
            @"(?:Mật khẩu tạm thời: )([^\r\n]+)|#token=([A-Fa-f0-9]{64})"))
            secrets.Add(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value);
        foreach (var secret in secrets.OrderByDescending(value => value.Length))
        {
            details = details.Replace(secret, "[REDACTED]", StringComparison.OrdinalIgnoreCase);
            details = details.Replace(Convert.ToBase64String(Encoding.UTF8.GetBytes(secret)), "[REDACTED]", StringComparison.Ordinal);
        }
        return details;
    }
}
