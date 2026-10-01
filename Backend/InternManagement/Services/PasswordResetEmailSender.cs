using System.Net;
using System.Net.Mail;

namespace InternManagement.Services;

public sealed class PasswordResetEmailSender(IConfiguration configuration)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(configuration["Email:SmtpHost"])
        && !string.IsNullOrWhiteSpace(configuration["Email:Username"])
        && !string.IsNullOrWhiteSpace(configuration["Email:Password"]);

    public async Task SendCodeAsync(string address, string code, CancellationToken cancellationToken)
    {
        var host = configuration["Email:SmtpHost"];
        var fromAddress = configuration["Email:FromAddress"];
        if (string.IsNullOrWhiteSpace(fromAddress)) fromAddress = configuration["Email:Username"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fromAddress))
            throw new InvalidOperationException("SMTP email settings are missing.");

        var port = int.TryParse(configuration["Email:SmtpPort"], out var parsedPort) ? parsedPort : 587;
        var enableSsl = !bool.TryParse(configuration["Email:EnableSsl"], out var parsedSsl) || parsedSsl;
        using var message = new MailMessage
        {
            From = new MailAddress(fromAddress, configuration["Email:FromName"] ?? "Intern Management System"),
            Subject = "Mã xác nhận đặt lại mật khẩu",
            Body = $"Mã xác nhận đặt lại mật khẩu của bạn là: {code}\n\nMã có hiệu lực trong 10 phút. Nếu bạn không yêu cầu đổi mật khẩu, hãy bỏ qua email này.",
            IsBodyHtml = false
        };
        message.To.Add(address);

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            Timeout = 15000
        };
        var username = configuration["Email:Username"];
        var password = configuration["Email:Password"];
        if (!string.IsNullOrWhiteSpace(username))
            client.Credentials = new NetworkCredential(username, password);

        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(message, cancellationToken);
    }
}
