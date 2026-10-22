using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Sockets;
using System.Text;
using InternManagement.DTOs;
using InternManagement.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

internal static class EmailSystemChecks
{
    internal sealed class UnavailableTransport:IEmailService
    {
        public int Attempts;
        public void ValidateConfiguration(){}
        public Task SendAsync(string recipient,EmailContent content,CancellationToken cancellationToken)
        {
            Attempts++;
            throw new PasswordRecoveryUnavailableException("SMTP test unavailable");
        }
    }
    public static async Task RunAsync(Action<bool,string> check,Func<TcpListener,Task<string>> capture)
    {
        using var smtp=new TcpListener(IPAddress.Loopback,0);smtp.Start();
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
            ["Smtp:Host"]="127.0.0.1",["Smtp:Port"]=((IPEndPoint)smtp.LocalEndpoint).Port.ToString(),["Smtp:EnableSsl"]="false",
            ["Smtp:FromAddress"]="system@company.vn",["Smtp:Username"]="smtp-login@sender.example",["Smtp:Password"]="Test-only-smtp-secret"
        }).Build();
        var logs=new Logs();var service=new EmailService(config,logs);
        var user=new UserResponse(1,"user@outlook.com","Nguyễn <script>alert(1)</script> & Mai","user@outlook.com","MENTOR",true);
        var account=EmailTemplates.Account(user,"Sample-temporary-9","https://portal.example/index.html");
        check(account.Html.Contains("&lt;script&gt;")&&!account.Html.Contains("<script>"),"account dynamic HTML escaped");
        check(account.Html.Contains("max-width:600px")&&account.Html.Contains("width=\"100%\"")&&account.Html.Contains("font-family:Arial"),"shared layout is fluid, 600px and email-safe");
        check(account.Html.Contains("CODEGYM")&&!account.Html.Contains("<img")&&account.Html.Contains("https://portal.example/index.html"),"text branding and valid CTA fallback avoid broken logo URLs");
        foreach(var domain in new[]{"gmail.com","outlook.com","yahoo.com","ictu.edu.vn","company.vn","university.edu","custom-domain.com"})
        {
            var recipient="user@"+domain;
            check(EmailService.ValidateRecipient(" "+recipient+" ")==recipient,"shared recipient validation accepts "+domain);
            var dto=new CreateUserRequest{FullName="Test",Email=" "+recipient+" ",Role="HR"};
            check(Validator.TryValidateObject(dto,new ValidationContext(dto),[],true)&&dto.Email==recipient,"account input accepts and trims "+domain);
            var captured=capture(smtp);await service.SendAsync(recipient,account,default);var mail=await captured.WaitAsync(TimeSpan.FromSeconds(10));
            check(mail.Contains("MAIL FROM:<system@company.vn>",StringComparison.OrdinalIgnoreCase)&&mail.Contains("RCPT TO:<"+recipient+">",StringComparison.OrdinalIgnoreCase)
                &&!mail.Contains("RCPT TO:<smtp-login@sender.example>",StringComparison.OrdinalIgnoreCase),"configured From and business To for "+domain);
            check(mail.Contains("multipart/alternative",StringComparison.OrdinalIgnoreCase)&&mail.Contains("text/plain",StringComparison.OrdinalIgnoreCase)&&mail.Contains("text/html",StringComparison.OrdinalIgnoreCase),"SMTP receives HTML and text alternatives for "+domain);
            check(mail.Contains("Mật khẩu tạm thời: Sample-temporary-9")&&mail.Contains("Hệ thống quản lý thực tập sinh"),"SMTP MIME keeps Vietnamese UTF-8 and temporary password intact");
        }
        foreach(var invalid in new[]{"","not-email","user@@example.com","user@example.com\r\nBcc:other@example.com","Name <user@example.com>",new string('a',255)+"@example.com"})
        {
            var rejected=false;try{EmailService.ValidateRecipient(invalid);}catch(ArgumentException){rejected=true;}check(rejected,"invalid recipient syntax rejected");
        }
        var invalidRecovery=new ForgotPasswordRequest{Email="not-email"};
        check(!Validator.TryValidateObject(invalidRecovery,new ValidationContext(invalidRecovery),[],true),"forgot password DTO rejects invalid email syntax");
        var invalidAccount=new CreateUserRequest{FullName="Test",Email="not-email",Role="HR"};
        check(!Validator.TryValidateObject(invalidAccount,new ValidationContext(invalidAccount),[],true),"account DTO rejects invalid email syntax");
        var approved=EmailTemplates.Review(user.FullName,"application","approved",null);
        var rejectedReview=EmailTemplates.Review(user.FullName,"application","rejected","<img src=x onerror=alert(1)> & lý do");
        check(approved.Html.Contains("ĐÃ DUYỆT")&&approved.Html.Contains("#168447")&&approved.Subject=="Hồ sơ thực tập của bạn đã được duyệt","approved template has actual status and success header");
        check(rejectedReview.Html.Contains("CHƯA ĐƯỢC DUYỆT")&&rejectedReview.Html.Contains("&lt;img")&&!rejectedReview.Html.Contains("<img src=x"),"rejection reason is actual and HTML encoded");
        var token=new string('A',64);var reset=EmailTemplates.Reset("https://portal.example/reset-password.html#token="+token,30);
        check(reset.Html.Contains("ĐẶT LẠI MẬT KHẨU")&&reset.PlainText.Contains("#token="+token)&&!reset.PlainText.Replace("https://portal.example/reset-password.html#token="+token,"").Contains(token),"reset token appears only inside reset URL");
        foreach(var email in new[]{approved,rejectedReview,reset})
        {
            var captured=capture(smtp);await service.SendAsync("person@university.edu",email,default);var mail=await captured.WaitAsync(TimeSpan.FromSeconds(10));
            check(mail.Contains(email.Subject)&&mail.Contains("text/html"),"Vietnamese subject and template survive SMTP "+email.Type);
        }
        check(!logs.Text.Contains("Sample-temporary-9")&&!logs.Text.Contains(token)&&!logs.Text.Contains("smtp-login@sender.example")&&!logs.Text.Contains("user@outlook.com")&&!logs.Text.Contains("Test-only-smtp-secret"),"success logs contain type/domain without credentials, recipient or body");
        smtp.Stop();
        foreach(var authFailure in new[]{true,false})
        {
            using var failure=new TcpListener(IPAddress.Loopback,0);failure.Start();config["Smtp:Port"]=((IPEndPoint)failure.LocalEndpoint).Port.ToString();
            var rejected=RejectSmtp(failure,authFailure);string? publicError=null;
            try{await service.SendAsync("person@outlook.com",account,default);}catch(PasswordRecoveryUnavailableException error){publicError=error.Message;}
            await rejected.WaitAsync(TimeSpan.FromSeconds(10));
            check(publicError is not null&&!publicError.Contains("Test-only-smtp-secret")&&!publicError.Contains("Sample-temporary-9")&&!publicError.Contains("535")&&!publicError.Contains("550"),authFailure?"SMTP authentication failure safely handled":"SMTP recipient/quota failure safely handled");
            check(!logs.Text.Contains("Test-only-smtp-secret")&&!logs.Text.Contains("Sample-temporary-9")&&!logs.Text.Contains("provider-private-message"),"failure logs do not expose provider text or secrets");
        }
    }
    private static async Task RejectSmtp(TcpListener listener,bool authFailure)
    {
        using var client=await listener.AcceptTcpClientAsync();await using var stream=client.GetStream();
        using var reader=new StreamReader(stream,Encoding.ASCII);using var writer=new StreamWriter(stream,Encoding.ASCII){AutoFlush=true,NewLine="\r\n"};
        await writer.WriteLineAsync("220 test SMTP");
        while(await reader.ReadLineAsync() is {} line)
        {
            if(line.StartsWith("EHLO")){await writer.WriteLineAsync("250-localhost");await writer.WriteLineAsync(authFailure?"250 AUTH LOGIN":"250 OK");}
            else if(line.StartsWith("AUTH")){await writer.WriteLineAsync("535 provider-private-message Test-only-smtp-secret");}
            else if(line.StartsWith("RCPT")){await writer.WriteLineAsync("550 provider-private-message Sample-temporary-9");}
            else if(line.StartsWith("QUIT")){await writer.WriteLineAsync("221 closing");break;}
            else await writer.WriteLineAsync("250 OK");
        }
    }
    private sealed class Logs:ILogger<EmailService>
    {
        public string Text="";public IDisposable? BeginScope<TState>(TState state) where TState:notnull=>null;
        public bool IsEnabled(LogLevel level)=>true;
        public void Log<TState>(LogLevel level,EventId id,TState state,Exception? error,Func<TState,Exception?,string> formatter)=>Text+=formatter(state,error)+"\n";
    }
}
