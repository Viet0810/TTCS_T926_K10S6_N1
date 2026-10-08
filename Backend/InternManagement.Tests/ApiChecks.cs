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
            await AttendanceReportChecks.RunAsync(http, connectionString, check);
            await InternAssignmentChecks.RunAsync(http, connectionString, check);
            await ChangePasswordChecks.RunAsync(http, connectionString, check);
            await Sprint2CompletionChecks.RunAsync(http, connectionString, check);
            using var programCreate = await http.PostAsJsonAsync("/api/program-schedule", new { startDate="2026-10-01", endDate="2026-10-08" });
            check(programCreate.StatusCode == HttpStatusCode.OK, "HR creates program using frontend dates");
            using var programData = System.Text.Json.JsonDocument.Parse(await programCreate.Content.ReadAsStringAsync());
            var programId = programData.RootElement.GetProperty("data").GetProperty("id").GetInt32();
            check(programData.RootElement.GetProperty("data").GetProperty("durationDays").GetInt32()==8, "program API returns inclusive duration");
            using var programs = await http.GetAsync("/api/program-schedule");
            check(programs.StatusCode == HttpStatusCode.OK, "HR loads program list");
            using var invalidProgram = await http.PostAsJsonAsync("/api/program-schedule", new { startDate="2026-10-08", endDate="2026-10-01" });
            check(invalidProgram.StatusCode == HttpStatusCode.BadRequest, "program rejects reversed dates");
            using var programDelete = await http.DeleteAsync($"/api/program-schedule/{programId}");
            check(programDelete.StatusCode == HttpStatusCode.OK, "HR deletes program");
            using var invalidReport = await http.GetAsync("/api/attendance/report?startDate=invalid");
            check(invalidReport.StatusCode == HttpStatusCode.BadRequest, "report API rejects malformed date filter");
            http.DefaultRequestHeaders.Authorization = null;
            using var anonymousPrograms = await http.GetAsync("/api/program-schedule");
            check(anonymousPrograms.StatusCode == HttpStatusCode.Unauthorized, "program API rejects missing session");
            using var badRegistration = await http.PostAsJsonAsync("/api/auth/register", new { fullName="Test", email="test@yahoo.com", password="Abc@1234", phone="0912345678", school="Test", major="Test" });
            check(badRegistration.StatusCode == HttpStatusCode.BadRequest, "registration API enforces email domains");
            using var registration = await http.PostAsJsonAsync("/api/auth/register", new { fullName="Test", email="sprint2-api@ictu.edu.vn", password="Abc@1234", phone="0912345678", school="Test", major="Test", role="ADMIN" });
            check(registration.StatusCode == HttpStatusCode.Created, "registration API accepts ICTU email");
            using var registrationData = System.Text.Json.JsonDocument.Parse(await registration.Content.ReadAsStringAsync());
            check(registrationData.RootElement.GetProperty("role").GetString()=="INTERN", "registration cannot select ADMIN role");
            using var duplicateRegistration = await http.PostAsJsonAsync("/api/auth/register", new { fullName="Test", email="sprint2-api@ictu.edu.vn", password="Abc@1234", phone="0912345678", school="Test", major="Test" });
            check(duplicateRegistration.StatusCode == HttpStatusCode.Conflict, "registration reports duplicate email");
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
                && !diagnostics.Contains("Temporary-test-9") && !diagnostics.Contains("Replacement-test-9")
                && !diagnostics.Contains("PBKDF2-SHA256$") && !diagnostics.Contains("Bearer "),
                "backend request logs omit passwords, hashes and authorization headers");
        }
    }
}
