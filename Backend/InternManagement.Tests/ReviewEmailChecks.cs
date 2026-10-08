using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using InternManagement.Controllers;
using InternManagement.DTOs;
using InternManagement.Models;
using InternManagement.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

internal static class ReviewEmailChecks
{
    public static async Task RunAsync(IConfiguration config, Action<bool,string> check, Func<TcpListener,Task<string>> captureMail)
    {
        var hasher = new PasswordHasher(); var accounts = new AccountService(config,hasher);
        var hr = await accounts.CreateAsync(new CreateUserRequest { FullName="Review mail HR",Email="review-mail-hr@gmail.com",Role="HR",Password="Original-test-9" },"HR",default);
        await accounts.ChangePasswordAsync(hr.Id,new ChangePasswordRequest { CurrentPassword="Original-test-9",NewPassword="Review-test-9",ConfirmPassword="Review-test-9" },default);
        var user = await accounts.AuthenticateAsync(new LoginRequest(hr.Email,"Review-test-9"),default);
        var interns = new InternService(config);
        var intern = await interns.CreateAsync(new CreateInternRequest { FullName="Review mail Intern",Email="review-owner@ictu.edu.vn",Phone="0912345678",School="ICTU",Major="IT" },default);
        var tokens = new AuthTokenService(new EphemeralDataProtectionProvider(),config);
        var context = new DefaultHttpContext(); context.Request.Path="/api/document-reviews";
        context.Request.Headers.Authorization="Bearer " + tokens.Issue(user!);
        var controller = new DocumentReviewsController(new InternDocumentService(config),new RequestAuthorizationService(tokens,new RolePermissionService()),
            interns,new PasswordResetService(config,hasher,NullLogger<PasswordResetService>.Instance),NullLogger<DocumentReviewsController>.Instance)
            { ControllerContext=new ControllerContext { HttpContext=context } };
        await using var sql = new SqlConnection(config.GetConnectionString("InternManagement")); await sql.OpenAsync();
        await using var command = sql.CreateCommand();
        command.Parameters.AddWithValue("@id",intern.Id);
        var originalPort=config["Smtp:Port"];
        using var smtp=new TcpListener(IPAddress.Loopback,0); smtp.Start();
        config["Smtp:Port"]=((IPEndPoint)smtp.LocalEndpoint).Port.ToString();
        try
        {
            foreach(var kind in new[]{"cv","application"})
            {
                command.CommandText="INSERT INTO dbo.InternDocuments(InternId,Kind,FileName,Content) VALUES(@id,@kind,'review.pdf',0x25504446)";
                command.Parameters.AddWithValue("@kind",kind); await command.ExecuteNonQueryAsync();
                command.CommandText="SELECT Version FROM dbo.InternDocuments WHERE InternId=@id AND Kind=@kind";
                var version=Convert.ToBase64String((byte[])(await command.ExecuteScalarAsync())!);
                await controller.List(default); await controller.Download(intern.Id,kind,default);
                check(!smtp.Pending(),"viewing review queue/document sends no email");
                var mailTask=captureMail(smtp);
                var status=kind=="cv"?"approved":"rejected"; var reason=kind=="cv"?null:"Please update application";
                var result=await controller.Review(intern.Id,kind,new ReviewDocumentRequest(status,reason,version),default) as OkObjectResult;
                check(result is not null && JsonSerializer.SerializeToElement(result.Value).GetProperty("emailSent").GetBoolean(),status+" commits review and sends email");
                var mail=await mailTask.WaitAsync(TimeSpan.FromSeconds(10));
                check(mail.Contains("RCPT TO:<"+intern.Email+">",StringComparison.OrdinalIgnoreCase)
                    && mail.Contains("MAIL FROM:<"+config["Smtp:FromAddress"]+">",StringComparison.OrdinalIgnoreCase),"review email uses intern recipient and shared system sender");
                check(mail.Contains(status=="approved"?"Approved":"Rejected") && (reason is null || mail.Contains(reason)),"review email includes actual result/rejection reason");
                var repeat=await controller.Review(intern.Id,kind,new ReviewDocumentRequest(status,reason,version),default);
                check(repeat is ConflictObjectResult && !smtp.Pending(),"repeated decision sends no duplicate email");
                command.Parameters.RemoveAt("@kind");
            }
            smtp.Stop();
            command.CommandText="UPDATE dbo.InternDocuments SET ReviewStatus='pending' WHERE InternId=@id AND Kind='cv'; SELECT Version FROM dbo.InternDocuments WHERE InternId=@id AND Kind='cv'";
            var nextVersion=Convert.ToBase64String((byte[])(await command.ExecuteScalarAsync())!);
            var failed=await controller.Review(intern.Id,"cv",new ReviewDocumentRequest("approved",null,nextVersion),default) as OkObjectResult;
            check(failed is not null && !JsonSerializer.SerializeToElement(failed.Value).GetProperty("emailSent").GetBoolean(),"SMTP failure reports email failure without undoing review");
            command.CommandText="SELECT ReviewStatus FROM dbo.InternDocuments WHERE InternId=@id AND Kind='cv'";
            check((string)(await command.ExecuteScalarAsync())! == "approved","review persists despite SMTP failure");
        }
        finally
        {
            config["Smtp:Port"]=originalPort;
            command.CommandText="DELETE FROM dbo.Interns WHERE Id=@id; DELETE FROM dbo.Users WHERE Id=@hrId";
            command.Parameters.AddWithValue("@hrId",hr.Id);
            await command.ExecuteNonQueryAsync();
        }
    }
}
