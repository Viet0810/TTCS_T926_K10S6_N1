using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
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

internal static class AccountEmailChecks
{
    public static async Task RunAsync(IConfiguration config, Action<bool,string> check,
        Func<TcpListener,Task<string>> captureMail)
    {
        var redact = typeof(PasswordResetService).GetMethod("RedactMailError", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var privateToken = new string('A', 64);
        var privatePassword = "Example-temporary-9";
        var privateSmtpPassword = "Example-app-password";
        var sanitized = (string)redact.Invoke(null, new object[] {
            "535 authentication failed: " + privateSmtpPassword + " " + privatePassword + " " + privateToken + " "
                + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(privateSmtpPassword)),
            privateSmtpPassword, "Mật khẩu tạm thời: " + privatePassword + "\n#token=" + privateToken })!;
        check(sanitized.Contains("535 authentication failed") && !sanitized.Contains(privatePassword)
            && !sanitized.Contains(privateSmtpPassword) && !sanitized.Contains(privateToken)
            && !sanitized.Contains(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(privateSmtpPassword))),
            "SMTP diagnostics preserve error detail while redacting passwords and tokens");
        var hasher = new PasswordHasher();
        var generated = Enumerable.Range(0,500).Select(_ => TemporaryPasswordGenerator.Generate()).ToArray();
        check(generated.All(value => value.Length == 10 && new StrongPasswordAttribute().IsValid(value))
            && generated.Distinct().Count() == generated.Length, "temporary generator produces randomized strong 10-character passwords");
        var accounts = new AccountService(config, hasher);
        var admin = await accounts.CreateAsync(new CreateUserRequest { FullName="Email test admin", Email="email-admin@gmail.com", Password="Account-test-9", Role="ADMIN" }, "ADMIN", default);
        var tokens = new AuthTokenService(new EphemeralDataProtectionProvider(), config);
        var auth = new RequestAuthorizationService(tokens, new RolePermissionService());
        var recovery = new PasswordResetService(config, hasher, NullLogger<PasswordResetService>.Instance);
        UsersController Controller(string role)
        {
            var context = new DefaultHttpContext();
            context.Request.Headers.Authorization = "Bearer " + tokens.Issue(new User { Id=admin.Id, Username=admin.Username, Role=role, PasswordHash=hasher.Hash("unused") });
            return new UsersController(accounts, auth, recovery, NullLogger<UsersController>.Instance) { ControllerContext=new ControllerContext { HttpContext=context } };
        }
        await using var sql = new SqlConnection(config.GetConnectionString("InternManagement"));
        await sql.OpenAsync();
        async Task<object?> Query(string query, params (string Name, object Value)[] args)
        {
            await using var command = sql.CreateCommand(); command.CommandText=query;
            foreach (var arg in args) command.Parameters.AddWithValue(arg.Name,arg.Value);
            return await command.ExecuteScalarAsync();
        }
        var adminHash = (string)(await Query("SELECT PasswordHash FROM dbo.Users WHERE Id=@id", ("@id",admin.Id)))!;
        var controller = Controller("ADMIN");
        controller.Request.Headers.Authorization = "Bearer " + tokens.Issue(new User { Id=admin.Id, Username=admin.Username, Role="ADMIN", PasswordHash=adminHash });
        var originalPort = config["Smtp:Port"];
        using var smtp = new TcpListener(IPAddress.Loopback,0); smtp.Start();
        config["Smtp:Port"] = ((IPEndPoint)smtp.LocalEndpoint).Port.ToString();
        foreach (var role in new[] { "HR", "MENTOR" })
        {
            var request = new CreateUserRequest { FullName="Email account test", Email=$"setup-{role.ToLowerInvariant()}@gmail.com", Password="Account-test-9", Role=role };
            var mailTask = captureMail(smtp);
            var result = await controller.CreateUser(request, default) as CreatedAtActionResult;
            var data = JsonSerializer.SerializeToElement(result!.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            check(data.GetProperty("accountCreated").GetBoolean() && data.GetProperty("emailSent").GetBoolean(), role+" creation reports account and email success");
            var mail = await mailTask.WaitAsync(TimeSpan.FromSeconds(10));
            var initialTemporary = Regex.Match(mail, "Mật khẩu tạm thời: ([^\r\n]+)").Groups[1].Value.Trim();
            check(initialTemporary.Length == 10 && new StrongPasswordAttribute().IsValid(initialTemporary)
                && initialTemporary != request.Password, "created account uses generated strong 10-character temporary password");
            check(Regex.IsMatch(mail, @"(?im)^To:\s*" + Regex.Escape(request.Email) + @"\s*$")
                && mail.Contains("RCPT TO:<" + request.Email + ">", StringComparison.OrdinalIgnoreCase),
                role + " recipient header and SMTP envelope use created account email");
            check(mail.Contains("MAIL FROM:<" + config["Smtp:FromAddress"] + ">", StringComparison.OrdinalIgnoreCase)
                && !mail.Contains("RCPT TO:<" + config["Smtp:FromAddress"] + ">", StringComparison.OrdinalIgnoreCase),
                "configured system sender is not used as recipient");
            check((await accounts.GetByIdAsync(data.GetProperty("data").GetProperty("id").GetInt32(),default))!.Email == request.Email,
                "database email matches admin input");
            check(mail.Contains(request.Email) && mail.Contains(role=="MENTOR"?"Mentor":"HR"), role+" email contains account credentials");
            check(data.GetProperty("data").GetProperty("mustChangePassword").GetBoolean()
                && !data.ToString().Contains(initialTemporary), "temporary account requires password change without exposing password in API");
            var userId = data.GetProperty("data").GetProperty("id").GetInt32();
            var storedHash = (string)(await Query("SELECT PasswordHash FROM dbo.Users WHERE Id=@id", ("@id",userId)))!;
            check(storedHash != initialTemporary && hasher.Verify(initialTemporary, storedHash), "database stores only verifiable password hash");
            check((await accounts.AuthenticateAsync(new LoginRequest(request.Email,initialTemporary),default))?.MustChangePassword == true,
                "emailed temporary password authenticates and requires first-login password change");
            var count = Convert.ToInt32(await Query("SELECT COUNT(*) FROM dbo.PasswordResetTokens"));
            check(await controller.CreateUser(request, default) is ConflictObjectResult
                && Convert.ToInt32(await Query("SELECT COUNT(*) FROM dbo.PasswordResetTokens"))==count, "duplicate account creates no email token");
            var resendMailTask = captureMail(smtp);
            var resend = await controller.ResendLoginEmail(userId, default) as OkObjectResult;
            check(JsonSerializer.SerializeToElement(resend!.Value).GetProperty("emailSent").GetBoolean(), "admin resend uses existing SMTP service");
            var resendMail = await resendMailTask.WaitAsync(TimeSpan.FromSeconds(10));
            var temporary = Regex.Match(resendMail, "Mật khẩu tạm thời: ([^\r\n]+)").Groups[1].Value.Trim();
            var replacementHash = (string)(await Query("SELECT PasswordHash FROM dbo.Users WHERE Id=@id", ("@id",userId)))!;
            check(temporary.Length == 10 && new StrongPasswordAttribute().IsValid(temporary) && temporary != initialTemporary && hasher.Verify(temporary, replacementHash)
                && !hasher.Verify(initialTemporary, replacementHash), "resend rotates strong 10-character temporary password");
            check(!JsonSerializer.Serialize(resend.Value).Contains(temporary), "resend response contains no temporary password");
            if (role == "HR")
            {
                var resetMailTask = captureMail(smtp);
                await recovery.RequestAsync(request.Email, default);
                var resetMail = await resetMailTask.WaitAsync(TimeSpan.FromSeconds(10));
                var resetToken = Regex.Match(resetMail, "#token=([A-F0-9]{64})").Groups[1].Value;
                check(await recovery.ResetAsync(resetToken, "Recovered-account-9", default), "existing recovery works for temporary account");
                check(!(await accounts.GetByIdAsync(userId, default))!.MustChangePassword, "password reset clears temporary-password requirement");
            }
        }
        smtp.Stop(); // Connection refused exercises actual SMTP failure and token cleanup.
        var failed = await controller.CreateUser(new CreateUserRequest { FullName="SMTP failure", Email="setup-failure@gmail.com", Password="Account-test-9", Role="HR" },default) as CreatedAtActionResult;
        var failure = JsonSerializer.SerializeToElement(failed!.Value,new JsonSerializerOptions(JsonSerializerDefaults.Web));
        check(failure.GetProperty("accountCreated").GetBoolean() && !failure.GetProperty("emailSent").GetBoolean()
            && await accounts.GetByIdAsync(failure.GetProperty("data").GetProperty("id").GetInt32(),default) is not null,
            "SMTP failure preserves created account and reports email failure");
        check(Convert.ToInt32(await Query("SELECT COUNT(*) FROM dbo.PasswordResetTokens WHERE UserId=@id", ("@id",failure.GetProperty("data").GetProperty("id").GetInt32())))==0, "failed delivery removes setup token");
        var failedId = failure.GetProperty("data").GetProperty("id").GetInt32();
        var failedResend = await controller.ResendLoginEmail(failedId,default) as OkObjectResult;
        check(!JsonSerializer.SerializeToElement(failedResend!.Value).GetProperty("emailSent").GetBoolean()
            && (await accounts.GetByIdAsync(failedId,default))!.MustChangePassword, "failed resend preserves account with required temporary password change");
        config["Smtp:Port"]=originalPort;
        var intern = await controller.CreateUser(new CreateUserRequest { FullName="No welcome email", Email="setup-intern@gmail.com", Password="Account-test-9", Role="INTERN" },default) as CreatedAtActionResult;
        check(JsonSerializer.SerializeToElement(intern!.Value).GetProperty("emailSent").ValueKind==JsonValueKind.Null, "INTERN account skips setup email");
        var hr = await accounts.GetByIdAsync(Convert.ToInt32(await Query("SELECT Id FROM dbo.Users WHERE Email='setup-hr@gmail.com'")),default);
        var hrHash=(string)(await Query("SELECT PasswordHash FROM dbo.Users WHERE Id=@id",("@id",hr!.Id)))!;
        controller.Request.Headers.Authorization="Bearer "+tokens.Issue(new User { Id=hr.Id, Username=hr.Username, Role="HR", PasswordHash=hrHash });
        var denied=await controller.CreateUser(new CreateUserRequest { FullName="Denied",Email="denied@gmail.com",Password="Account-test-9",Role="HR" },default) as ObjectResult;
        check(denied?.StatusCode==403, "non-admin cannot create account or trigger setup email");
    }
}
