using System.Data;
using InternManagement.DTOs;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace InternManagement.Services;

public sealed class PasswordResetService(
    IConfiguration configuration,
    PasswordHasher passwords,
    ILogger<PasswordResetService> logger,
    IEmailService? emailService = null)
{
    private readonly string connectionString = configuration.GetConnectionString("InternManagement")
        ?? throw new InvalidOperationException("Chưa cấu hình kết nối cơ sở dữ liệu.");

    private readonly IEmailService emailTransport=emailService ?? new EmailService(configuration,Microsoft.Extensions.Logging.Abstractions.NullLogger<EmailService>.Instance);

    public Task RequestAsync(string email, CancellationToken cancellationToken)
        => RequestCoreAsync(email, cancellationToken);

    public Task SendReviewResultAsync(string recipient,string fullName,string kind,string status,string? reason,CancellationToken ct)
        =>emailTransport.SendAsync(recipient,EmailTemplates.Review(fullName,kind,status,reason),ct);

    public async Task<bool> SendAccountCreatedAsync(UserResponse user,string temporaryPassword,CancellationToken ct)
    {
        if(user.Role is not ("HR" or "MENTOR"))return false;
        var loginUrl=configuration["Frontend:LoginUrl"];
        if(string.IsNullOrWhiteSpace(loginUrl)&&Uri.TryCreate(configuration["PasswordReset:ResetPageUrl"],UriKind.Absolute,out var resetPage))
            loginUrl=new Uri(resetPage,"index.html").AbsoluteUri;
        await emailTransport.SendAsync(user.Email,EmailTemplates.Account(user,temporaryPassword,loginUrl),ct);
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
        emailTransport.ValidateConfiguration();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var lookup = connection.CreateCommand();
        lookup.CommandText = "SELECT Id,Email FROM dbo.Users WHERE Email = @email";
        lookup.Parameters.Add("@email", SqlDbType.NVarChar, 254).Value = email.Trim();
        int userId;string recipient;
        await using(var reader=await lookup.ExecuteReaderAsync(cancellationToken))
        {
            if(!await reader.ReadAsync(cancellationToken))return false;
            userId=reader.GetInt32(0);recipient=reader.GetString(1);
        }

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
        var message=EmailTemplates.Reset(link,lifetime);
        try
        {
            await emailTransport.SendAsync(recipient,message,cancellationToken);
            return true;
        }
        catch (Exception error)
        {
            // A failed delivery must never leave a usable reset link in storage.
            await using var remove = connection.CreateCommand();
            remove.CommandText = "DELETE FROM dbo.PasswordResetTokens WHERE TokenHash = @tokenHash";
            remove.Parameters.Add("@tokenHash", SqlDbType.Binary, 32).Value = tokenHash;
            await remove.ExecuteNonQueryAsync(CancellationToken.None);
            logger.LogWarning("Password reset email delivery failed. FailureType {FailureType}. Generic recovery response retained.",error.GetType().Name);
            cancellationToken.ThrowIfCancellationRequested();
            // Delivery failure must not distinguish an existing account from an unknown address.
            return false;
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
