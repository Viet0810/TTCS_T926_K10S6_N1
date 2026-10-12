using System.Diagnostics;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;

namespace InternManagement.Services;

public sealed record EmailContent(string Type,string Subject,string PlainText,string Html);
public interface IEmailService
{
    void ValidateConfiguration();
    Task SendAsync(string recipient,EmailContent content,CancellationToken ct);
}

public sealed class EmailService(IConfiguration configuration,ILogger<EmailService> logger):IEmailService
{
    public static string ValidateRecipient(string recipient)
    {
        var address=recipient?.Trim();
        if(string.IsNullOrEmpty(address)||address.Length>254||address.Any(char.IsControl)
            ||!MailAddress.TryCreate(address,out var parsed)||parsed.Address!=address)
            throw new ArgumentException("Địa chỉ email không hợp lệ.",nameof(recipient));
        return address;
    }
    private (string Host,int Port,bool Ssl,string Sender) Settings()
    {
        var smtp=configuration.GetSection("Smtp");
        try
        {
            var host=smtp["Host"];var sender=smtp["FromAddress"];
            if(string.IsNullOrWhiteSpace(host)||string.IsNullOrWhiteSpace(sender))
                throw new PasswordRecoveryUnavailableException("Chưa cấu hình SMTP Host hoặc địa chỉ email gửi.");
            var port=smtp.GetValue("Port",587);var ssl=smtp.GetValue("EnableSsl",true);
            if(port is <1 or >65535)throw new PasswordRecoveryUnavailableException("Cổng SMTP không hợp lệ.");
            sender=ValidateRecipient(sender);
            if(host.Equals("smtp.gmail.com",StringComparison.OrdinalIgnoreCase)
                &&(port!=587||!ssl||string.IsNullOrWhiteSpace(smtp["Username"])||string.IsNullOrWhiteSpace(smtp["Password"])))
                throw new PasswordRecoveryUnavailableException("Gmail SMTP cần port 587, SSL, tài khoản gửi và App Password.");
            return(host.Trim(),port,ssl,sender);
        }
        catch(PasswordRecoveryUnavailableException){throw;}
        catch(Exception error)
        {
            logger.LogWarning("Email configuration failed. FailureType {FailureType}, Trace {TraceId}.",error.GetType().Name,Activity.Current?.TraceId.ToString());
            throw new PasswordRecoveryUnavailableException("Cấu hình SMTP không hợp lệ. Vui lòng liên hệ quản trị viên.");
        }
    }
    public void ValidateConfiguration()=>Settings();
    public async Task SendAsync(string recipient,EmailContent content,CancellationToken ct)
    {
        var address=ValidateRecipient(recipient);var domain=new MailAddress(address).Host;
        var smtp=configuration.GetSection("Smtp");
        try
        {
            var settings=Settings();
            using var message=new MailMessage
            {
                From=new MailAddress(settings.Sender,"CODEGYM | Hệ thống quản lý thực tập sinh",Encoding.UTF8),
                Subject=content.Subject,SubjectEncoding=Encoding.UTF8,HeadersEncoding=Encoding.UTF8,
                Body=content.PlainText,BodyEncoding=Encoding.UTF8,BodyTransferEncoding=TransferEncoding.Base64
            };
            message.To.Add(new MailAddress(address));
            var html=AlternateView.CreateAlternateViewFromString(content.Html,Encoding.UTF8,MediaTypeNames.Text.Html);
            html.TransferEncoding=TransferEncoding.Base64;message.AlternateViews.Add(html);
            using var client=new SmtpClient(settings.Host,settings.Port)
            {
                EnableSsl=settings.Ssl,UseDefaultCredentials=false,
                DeliveryMethod=SmtpDeliveryMethod.Network,Timeout=15000
            };
            if(!string.IsNullOrWhiteSpace(smtp["Username"]))client.Credentials=new NetworkCredential(smtp["Username"],smtp["Password"]);
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(15));
            await client.SendMailAsync(message,timeout.Token);
            logger.LogInformation("Email {EmailType} sent. RecipientDomain {RecipientDomain}, Trace {TraceId}.",content.Type,domain,Activity.Current?.TraceId.ToString());
        }
        catch(Exception error)
        {
            // Provider text can echo credentials, a recipient or the whole message. Never log it.
            logger.LogWarning("Email {EmailType} failed. RecipientDomain {RecipientDomain}, FailureType {FailureType}, SMTP status {SmtpStatus}, Trace {TraceId}.",
                content.Type,domain,error.GetType().Name,error is SmtpException failure?failure.StatusCode.ToString():"Unavailable",Activity.Current?.TraceId.ToString());
            if(error is PasswordRecoveryUnavailableException)throw;
            throw new PasswordRecoveryUnavailableException("Dịch vụ email hiện không khả dụng. Vui lòng kiểm tra cấu hình SMTP hoặc thử lại sau.");
        }
    }
}
