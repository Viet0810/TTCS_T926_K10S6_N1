using System.Security.Cryptography;
using System.Text;
using System.Net.Mail;
using InternManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;

namespace InternManagement.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class PasswordResetController : ControllerBase
{
    private readonly string connectionString;
    private readonly PasswordResetEmailSender emailSender;
    private readonly PasswordHasher passwords;
    private readonly ILogger<PasswordResetController> logger;
    private readonly IDataProtector codeProtector;

    public PasswordResetController(IConfiguration configuration, PasswordResetEmailSender emailSender,
        PasswordHasher passwords, IDataProtectionProvider protectionProvider, ILogger<PasswordResetController> logger)
    {
        connectionString = configuration.GetConnectionString("InternManagement")
            ?? throw new InvalidOperationException("ConnectionStrings:InternManagement is missing.");
        this.emailSender = emailSender;
        this.passwords = passwords;
        this.logger = logger;
        codeProtector = protectionProvider.CreateProtector("InternManagement.PasswordResetOtp.v1");
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> RequestCode(EmailRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || !MailAddress.TryCreate(request.Email.Trim(), out _))
            return BadRequest(new { message = "Vui lòng nhập email hợp lệ." });
        if (!emailSender.IsConfigured)
            return StatusCode(503, new { message = "Chức năng gửi email chưa được cấu hình. Vui lòng liên hệ quản trị viên." });

        var email = request.Email.Trim();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var find = connection.CreateCommand();
        find.CommandText = "SELECT Id FROM dbo.Users WHERE Email = @email";
        find.Parameters.AddWithValue("@email", email);
        var result = await find.ExecuteScalarAsync(cancellationToken);
        // Always return the same message for registered and unknown emails.
        if (result is null) return Ok(new { message = GenericRequestMessage });

        var userId = Convert.ToInt32(result);
        await using var check = connection.CreateCommand();
        check.CommandText = "SELECT COUNT(1) FROM dbo.PasswordResetRequests WHERE UserId=@id AND CreatedAt > DATEADD(SECOND,-60,SYSUTCDATETIME())";
        check.Parameters.AddWithValue("@id", userId);
        if (Convert.ToInt32(await check.ExecuteScalarAsync(cancellationToken)) > 0)
            return Ok(new { message = GenericRequestMessage });

        var otp = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        await using (var save = connection.CreateCommand())
        {
            save.CommandText = """
                IF EXISTS (SELECT 1 FROM dbo.PasswordResetRequests WHERE UserId=@id)
                    UPDATE dbo.PasswordResetRequests SET CodeProtected=@code, ExpiresAt=DATEADD(MINUTE,10,SYSUTCDATETIME()),
                        Attempts=0, ResetTokenHash=NULL, ResetTokenExpiresAt=NULL, CreatedAt=SYSUTCDATETIME() WHERE UserId=@id;
                ELSE
                    INSERT INTO dbo.PasswordResetRequests (UserId, CodeProtected, ExpiresAt) VALUES (@id,@code,DATEADD(MINUTE,10,SYSUTCDATETIME()));
                """;
            save.Parameters.AddWithValue("@id", userId);
            save.Parameters.AddWithValue("@code", codeProtector.Protect(otp));
            await save.ExecuteNonQueryAsync(cancellationToken);
        }

        try
        {
            await emailSender.SendCodeAsync(email, otp, cancellationToken);
        }
        catch (Exception error)
        {
            logger.LogError(error, "Could not send a password reset code to {EmailAddress}.", email);
            await using var invalidate = connection.CreateCommand();
            invalidate.CommandText = "DELETE FROM dbo.PasswordResetRequests WHERE UserId=@id";
            invalidate.Parameters.AddWithValue("@id", userId);
            await invalidate.ExecuteNonQueryAsync(cancellationToken);
            // Keep the same response for known and unknown addresses to avoid account enumeration.
            return Ok(new { message = GenericRequestMessage });
        }

        return Ok(new { message = GenericRequestMessage });
    }

    [HttpPost("verify-reset-code")]
    public async Task<IActionResult> VerifyCode(VerifyCodeRequest request, CancellationToken cancellationToken)
    {
        if (!ValidEmail(request.Email) || string.IsNullOrWhiteSpace(request.Code) || request.Code.Length != 6 || request.Code.Any(c => !char.IsDigit(c)))
            return BadRequest(new { message = "Email hoặc mã OTP không hợp lệ." });

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT r.UserId, r.CodeProtected FROM dbo.PasswordResetRequests r
            INNER JOIN dbo.Users u ON u.Id=r.UserId
            WHERE u.Email=@email AND r.ExpiresAt>SYSUTCDATETIME() AND r.Attempts<5;
            """;
        command.Parameters.AddWithValue("@email", request.Email!.Trim());
        int userId;
        string protectedCode;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken)) return Unauthorized(new { message = "Mã OTP không đúng, đã hết hạn hoặc vượt quá số lần thử." });
            userId = reader.GetInt32(0);
            protectedCode = reader.GetString(1);
        }

        await using (var increment = connection.CreateCommand())
        {
            increment.CommandText = "UPDATE dbo.PasswordResetRequests SET Attempts=Attempts+1 WHERE UserId=@id AND Attempts<5 AND ExpiresAt>SYSUTCDATETIME()";
            increment.Parameters.AddWithValue("@id", userId);
            if (await increment.ExecuteNonQueryAsync(cancellationToken) == 0)
                return Unauthorized(new { message = "Mã OTP không đúng, đã hết hạn hoặc vượt quá số lần thử." });
        }

        var expected = Encoding.UTF8.GetBytes(codeProtector.Unprotect(protectedCode));
        var actual = Encoding.UTF8.GetBytes(request.Code);
        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
            return Unauthorized(new { message = "Mã OTP không đúng, đã hết hạn hoặc vượt quá số lần thử." });

        var resetToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        await using var update = connection.CreateCommand();
        update.CommandText = "UPDATE dbo.PasswordResetRequests SET ResetTokenHash=@hash, ResetTokenExpiresAt=DATEADD(MINUTE,10,SYSUTCDATETIME()) WHERE UserId=@id AND ExpiresAt>SYSUTCDATETIME()";
        update.Parameters.AddWithValue("@id", userId);
        update.Parameters.AddWithValue("@hash", HashResetToken(resetToken));
        await update.ExecuteNonQueryAsync(cancellationToken);
        return Ok(new { message = "Xác minh email thành công.", resetToken });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        if (!ValidEmail(request.Email)) return BadRequest(new { message = "Email không hợp lệ." });
        if (string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length < 8)
            return BadRequest(new { message = "Mật khẩu mới phải có ít nhất 8 ký tự." });
        if (request.NewPassword != request.ConfirmPassword)
            return BadRequest(new { message = "Mật khẩu xác nhận không khớp." });
        if (string.IsNullOrWhiteSpace(request.ResetToken))
            return BadRequest(new { message = "Phiên xác nhận không hợp lệ. Hãy xác minh mã OTP lại." });

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using var updateUser = connection.CreateCommand();
            updateUser.Transaction = transaction;
            updateUser.CommandText = """
                UPDATE u SET PasswordHash=@passwordHash
                FROM dbo.Users u INNER JOIN dbo.PasswordResetRequests r ON r.UserId=u.Id
                WHERE u.Email=@email AND r.ResetTokenHash=@tokenHash AND r.ResetTokenExpiresAt>SYSUTCDATETIME();
                """;
            updateUser.Parameters.AddWithValue("@passwordHash", passwords.Hash(request.NewPassword!));
            updateUser.Parameters.AddWithValue("@email", request.Email!.Trim());
            updateUser.Parameters.AddWithValue("@tokenHash", HashResetToken(request.ResetToken!));
            if (await updateUser.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Unauthorized(new { message = "Phiên đặt lại mật khẩu đã hết hạn hoặc không hợp lệ. Hãy xác minh lại mã OTP." });
            }
            await using var delete = connection.CreateCommand();
            delete.Transaction = transaction;
            delete.CommandText = "DELETE r FROM dbo.PasswordResetRequests r INNER JOIN dbo.Users u ON u.Id=r.UserId WHERE u.Email=@email";
            delete.Parameters.AddWithValue("@email", request.Email.Trim());
            await delete.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Ok(new { message = "Đặt lại mật khẩu thành công. Bạn có thể đăng nhập bằng mật khẩu mới." });
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static bool ValidEmail(string? email) => !string.IsNullOrWhiteSpace(email) && MailAddress.TryCreate(email.Trim(), out _);
    private static string HashResetToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private const string GenericRequestMessage = "Nếu email này đã được đăng ký, mã xác nhận sẽ được gửi đến hộp thư của bạn.";
    public sealed record EmailRequest(string? Email);
    public sealed record VerifyCodeRequest(string? Email, string? Code);
    public sealed record ResetPasswordRequest(string? Email, string? ResetToken, string? NewPassword, string? ConfirmPassword);
}
