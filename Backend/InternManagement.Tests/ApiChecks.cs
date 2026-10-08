using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using InternManagement.Controllers;

internal static class ApiChecks
{
    public static async Task RunAsync(string connectionString, Action<bool,string> check)
    {
        using var reserve = new TcpListener(IPAddress.Loopback,0);
        reserve.Start();
        var port = ((IPEndPoint)reserve.LocalEndpoint).Port;
        reserve.Stop();
        var assembly = typeof(AuthController).Assembly.Location;
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(assembly)!
        };
        start.ArgumentList.Add(assembly);
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        start.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";
        start.Environment["ConnectionStrings__InternManagement"] = connectionString;
        start.Environment["Smtp__Host"] = "";
        start.Environment["Logging__LogLevel__Default"] = "Error";
        using var process = Process.Start(start) ?? throw new Exception("Could not start test API.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var http = new HttpClient { BaseAddress=new Uri($"http://127.0.0.1:{port}"), Timeout=TimeSpan.FromSeconds(5) };
        try
        {
            var ready = false;
            for (var attempt=0;attempt<40;attempt++)
            {
                if (process.HasExited) throw new Exception("Test API exited before becoming ready.");
                try { using var response = await http.GetAsync("/api/database/status"); if(response.IsSuccessStatusCode) {ready=true;break;} }
                catch(HttpRequestException) { }
                await Task.Delay(100);
            }
            check(ready,"application starts with routing and rate limiting enabled");
            await RolePermissionChecks.RunAsync(http, connectionString, check);
            await AccountChecks.RunAsync(http, connectionString, check);
            await DocumentChecks.RunAsync(http, connectionString, check);
            using var noSession = await http.GetAsync("/api/auth/me");
            check(noSession.StatusCode==HttpStatusCode.Unauthorized,"authenticated endpoints reject missing sessions");
            using var invalidEmail = await http.PostAsJsonAsync("/api/auth/forgot-password",new {email="not-an-email"});
            check(invalidEmail.StatusCode==HttpStatusCode.BadRequest,"forgot-password validates email on the server");
            using var missingEmail = await http.PostAsJsonAsync("/api/auth/forgot-password",new { });
            check(missingEmail.StatusCode==HttpStatusCode.BadRequest,"forgot-password rejects a missing email");
            using var malformedToken = await http.PostAsJsonAsync("/api/auth/reset-password",new {token="invalid",password="Long-password-9"});
            check(malformedToken.StatusCode==HttpStatusCode.BadRequest,"reset-password validates token format");
            using var shortPassword = await http.PostAsJsonAsync("/api/auth/reset-password",new {token=new string('A',64),password="short"});
            check(shortPassword.StatusCode==HttpStatusCode.BadRequest,"reset-password rejects passwords shorter than eight characters");
            using var noSmtp = await http.PostAsJsonAsync("/api/auth/forgot-password",new {email="auth-test@example.invalid"});
            check(noSmtp.StatusCode==HttpStatusCode.ServiceUnavailable,"email delivery configuration errors are returned as HTTP 503");
            using var unknownToken = await http.PostAsJsonAsync("/api/auth/reset-password",new {token=new string('A',64),password="Long-password-9"});
            check(unknownToken.StatusCode==HttpStatusCode.BadRequest,"unknown tokens produce a validation error through the API");
            var throttled=false;
            for(var attempt=0;attempt<11;attempt++)
            {
                using var response=await http.PostAsJsonAsync("/api/auth/forgot-password",new {email="not-an-email"});
                if(response.StatusCode==HttpStatusCode.TooManyRequests) { throttled=true;break; }
            }
            check(throttled,"repeated recovery requests receive HTTP 429");
        }
        finally
        {
            if(!process.HasExited) { process.Kill(entireProcessTree:true); await process.WaitForExitAsync(); }
            await Task.WhenAll(stdout,stderr);
            var diagnostics = await stdout;
            check(diagnostics.Contains("API failed.") && diagnostics.Contains("/api/users")
                && diagnostics.Contains("trace") && diagnostics.Contains("UTC"), "backend failure logs identify endpoint, trace and time");
            check(!diagnostics.Contains("Permission-test-9") && !diagnostics.Contains("Account-test-9")
                && !diagnostics.Contains("PBKDF2-SHA256$") && !diagnostics.Contains("Bearer "),
                "backend request logs omit passwords, hashes and authorization headers");
        }
    }
}
