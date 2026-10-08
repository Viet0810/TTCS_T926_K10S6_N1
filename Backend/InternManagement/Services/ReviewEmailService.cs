using System.Net;
using System.Net.Mail;
using System.Text;
using InternManagement.DTOs;

namespace InternManagement.Services;

public sealed class ReviewEmailService(IConfiguration configuration, ILogger<ReviewEmailService> logger)
{
    public async Task<ReviewNotificationResult> SendAsync(string recipient, string internName, string kind,
        string status, string? comment, CancellationToken cancellationToken)
    {
        var attemptedAt = DateTime.UtcNow;
        var smtp = configuration.GetSection("Smtp");
        var host = smtp["Host"];
        var sender = smtp["FromAddress"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(sender))
        {
            logger.LogWarning("Review email was not sent because SMTP host or sender address is not configured.");
            return new("failed", "Chưa cấu hình dịch vụ gửi email.", attemptedAt);
        }

        try
        {
            var port = smtp.GetValue("Port", 587);
            if (port is < 1 or > 65535) throw new InvalidOperationException("Cấu hình cổng SMTP không hợp lệ.");
            var approved = status == "approved";
            var documentName = kind == "cv" ? "CV" : "đơn xin thực tập";
            var subject = approved ? $"{documentName} đã được HR duyệt" : $"{documentName} cần bổ sung";
            var body = approved
                ? $"Chào {internName},\n\nHR đã duyệt {documentName} của bạn. Bạn có thể đăng nhập vào hệ thống để xem trạng thái hồ sơ."
                : $"Chào {internName},\n\nHR yêu cầu bạn bổ sung hoặc chỉnh sửa {documentName}.\nLý do: {comment}\n\nVui lòng đăng nhập vào hệ thống để nộp lại tài liệu.";
            using var message = new MailMessage
            {
                From = new MailAddress(sender, "Hệ thống quản lý thực tập sinh", Encoding.UTF8),
                Subject = subject,
                SubjectEncoding = Encoding.UTF8,
                BodyEncoding = Encoding.UTF8,
                Body = body
            };
            message.To.Add(new MailAddress(recipient));
            using var client = new SmtpClient(host, port)
            {
                EnableSsl = smtp.GetValue("EnableSsl", true),
                UseDefaultCredentials = false,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 15000
            };
            if (!string.IsNullOrWhiteSpace(smtp["Username"]))
                client.Credentials = new NetworkCredential(smtp["Username"], smtp["Password"]);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            await client.SendMailAsync(message, timeout.Token);
            return new("sent", $"Email đã gửi đến {recipient}.", attemptedAt);
        }
        catch (Exception error)
        {
            var failureCode = error is SmtpException smtpError
                ? smtpError.StatusCode.ToString()
                : error.GetType().Name;
            logger.LogWarning("Review email delivery failed ({FailureCode}).", failureCode);
            return new("failed", "Gửi email thất bại. Kiểm tra cấu hình SMTP và dùng nút Gửi lại email trong giao diện HR.", attemptedAt);
        }
    }
}
