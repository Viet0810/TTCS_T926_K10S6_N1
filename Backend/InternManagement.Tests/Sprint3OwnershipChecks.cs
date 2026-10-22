using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InternManagement.Services;
using Microsoft.Data.SqlClient;

internal static class Sprint3OwnershipChecks
{
    public static async Task RunAsync(HttpClient http, string connectionString, Action<bool, string> check,Func<Task>? restart=null)
    {
        await using var sql = new SqlConnection(connectionString);
        await sql.OpenAsync();
        await using var command = sql.CreateCommand();
        command.Parameters.AddWithValue("@hash", new PasswordHasher().Hash("Ownership-test-9"));
        command.CommandText = """
            INSERT dbo.Users(Username,FullName,Email,PasswordHash,Role) VALUES
                ('ownership-mentor-a','Mentor A','ownership-ma@example.org',@hash,'MENTOR'),
                ('ownership-mentor-b','Mentor B','ownership-mb@example.org',@hash,'MENTOR'),
                ('ownership-intern-a','Intern A','ownership-ia@example.org',@hash,'INTERN'),
                ('ownership-intern-b','Intern B','ownership-ib@example.org',@hash,'INTERN'),
                ('ownership-unmapped','Unmapped Intern','ownership-unmapped@example.org',@hash,'INTERN'),
                ('ownership-hr','HR','ownership-hr@example.org',@hash,'HR');
            INSERT dbo.Interns(FullName,Email,Phone,School,Major,MentorUserId)
            SELECT 'Intern A','ownership-ia@example.org','0912345678','ICTU','IT',Id
            FROM dbo.Users WHERE Username='ownership-mentor-a';
            INSERT dbo.Interns(FullName,Email,Phone,School,Major,MentorUserId)
            SELECT 'Intern B','ownership-ib@example.org','0912345678','ICTU','IT',Id
            FROM dbo.Users WHERE Username='ownership-mentor-b';
            -- Deliberately collide independent User/Intern identity domains.
            IF NOT EXISTS (SELECT 1 FROM dbo.Interns WHERE Id=(SELECT Id FROM dbo.Users WHERE Username='ownership-unmapped'))
            BEGIN
            SET IDENTITY_INSERT dbo.Interns ON;
            INSERT dbo.Interns(Id,FullName,Email,Phone,School,Major)
            SELECT Id,'Unrelated intern','ownership-collision@example.org','0912345678','ICTU','IT'
            FROM dbo.Users WHERE Username='ownership-unmapped';
            SET IDENTITY_INSERT dbo.Interns OFF;
            END;
            INSERT dbo.RolePermissions(Role,Permission)
            SELECT r.Role,p.Permission FROM (VALUES('MENTOR'),('HR')) r(Role)
            CROSS JOIN (VALUES('VIEW_FINAL_EVALUATIONS'),('UPDATE_FINAL_EVALUATIONS')) p(Permission)
            WHERE NOT EXISTS (SELECT 1 FROM dbo.RolePermissions rp WHERE rp.Role=r.Role AND rp.Permission=p.Permission);
            SELECT Id FROM dbo.Interns WHERE Email='ownership-ia@example.org';
            """;
        var internA = Convert.ToInt32(await command.ExecuteScalarAsync());
        command.Parameters.Clear();
        async Task<int> Scalar(string query)
        {
            command.CommandText = query;
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }
        var internB = await Scalar("SELECT Id FROM dbo.Interns WHERE Email='ownership-ib@example.org'");
        async Task Login(string username)
        {
            http.DefaultRequestHeaders.Authorization = null;
            using var response = await http.PostAsJsonAsync("/api/auth/login", new { username, password = "Ownership-test-9" });
            check(response.IsSuccessStatusCode, "ownership login " + username);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.RootElement.GetProperty("token").GetString());
        }
        async Task<JsonElement> Get(string path)
        {
            using var response = await http.GetAsync(path);
            check(response.StatusCode == HttpStatusCode.OK, "ownership GET " + path);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return json.RootElement.Clone();
        }
        var evaluation = new { period = "Sprint 3", attendance = 8, attitude = 8, professionalSkills = 8, workResult = 8, finalScore = 8, classification = "Good", comment = "Ownership regression" };
        const string evaluations = "/api/FinalEvaluations";
        await Login("ownership-mentor-a");
        using var created = await http.PostAsJsonAsync($"{evaluations}/{internA}", evaluation);
        check(created.StatusCode == HttpStatusCode.Created, "US19 own Mentor creates evaluation");
        using var updated = await http.PutAsJsonAsync($"{evaluations}/{internA}", evaluation);
        check(updated.StatusCode == HttpStatusCode.OK, "US19 own Mentor updates evaluation");
        await Get($"{evaluations}/{internA}");
        var list = await Get(evaluations);
        check(list.GetArrayLength() == 1 && list[0].GetProperty("internId").GetInt32() == internA, "US19 list contains only current Mentor interns");
        check((await Get(evaluations + "/statistics")).GetProperty("totalInterns").GetInt32() == 1, "US19 statistics exclude unrelated interns");
        using var foreignCreate = await http.PostAsJsonAsync($"{evaluations}/{internB}", evaluation);
        check(foreignCreate.StatusCode == HttpStatusCode.Forbidden, "US19 Mentor cannot create for another Mentor's Intern");
        await Login("ownership-mentor-b");
        using var foreignRead = await http.GetAsync($"{evaluations}/{internA}");
        using var foreignUpdate = await http.PutAsJsonAsync($"{evaluations}/{internA}", evaluation);
        check(foreignRead.StatusCode == HttpStatusCode.Forbidden && foreignUpdate.StatusCode == HttpStatusCode.Forbidden, "US19 other Mentor read/update return 403");
        // Current US30 assignment is authoritative, including after reassignment.
        command.CommandText = $"UPDATE dbo.Interns SET MentorUserId=(SELECT Id FROM dbo.Users WHERE Username='ownership-mentor-b') WHERE Id={internA}";
        await command.ExecuteNonQueryAsync();
        await Login("ownership-mentor-a");
        using var oldRead = await http.GetAsync($"{evaluations}/{internA}");
        using var oldUpdate = await http.PutAsJsonAsync($"{evaluations}/{internA}", evaluation);
        check(oldRead.StatusCode == HttpStatusCode.Forbidden && oldUpdate.StatusCode == HttpStatusCode.Forbidden, "US19 former Mentor loses access after reassignment");
        await Login("ownership-mentor-b");
        using var reassigned = await http.PutAsJsonAsync($"{evaluations}/{internA}", evaluation);
        check(reassigned.StatusCode == HttpStatusCode.OK, "US19 newly assigned Mentor can update");
        await Login("ownership-hr");
        var totalInterns = await Scalar("SELECT COUNT(*) FROM dbo.Interns");
        var hrBeforeSummary=await Get(evaluations);
        check(hrBeforeSummary.GetArrayLength() == totalInterns, "US19 HR retains all-intern view");
        var mentorReadonly=hrBeforeSummary.EnumerateArray().Single(x=>x.GetProperty("internId").GetInt32()==internA);
        check(mentorReadonly.GetProperty("status").GetString()=="Chưa đánh giá"
            && mentorReadonly.GetProperty("mentorScore").GetDecimal()==8
            && mentorReadonly.GetProperty("mentorAttendance").GetDecimal()==8,
            "US20 HR sees full Mentor evaluation read-only while HR summary remains pending");
        check((await Get(evaluations + "/statistics")).GetProperty("totalInterns").GetInt32() == totalInterns, "US19 HR retains aggregate statistics");
        using var hrCreate = await http.PostAsJsonAsync($"{evaluations}/{internB}", evaluation);
        using var hrUpdate = await http.PutAsJsonAsync($"{evaluations}/{internA}", evaluation);
        check(hrCreate.StatusCode == HttpStatusCode.Created && hrUpdate.StatusCode == HttpStatusCode.OK, "US19 HR retains existing create/update permissions");
        check(await Scalar($"SELECT COUNT(*) FROM dbo.FinalEvaluations WHERE InternId={internA} AND EvaluationSource='MENTOR'")==2,"US19 Mentor A/B evaluations remain separate after reassignment");
        check(await Scalar($"SELECT COUNT(*) FROM dbo.FinalEvaluations WHERE InternId={internA} AND EvaluationSource='HR'")==1,"US20 HR summary never overwrites Mentor rows");
        check(await Scalar($"SELECT COUNT(*) FROM dbo.FinalEvaluations WHERE CreatedByUserId IS NULL")==0,"US19/20 author audit persisted");
        await Login("ownership-intern-a");
        using var internForeignRead = await http.GetAsync($"{evaluations}/{internB}");
        check(internForeignRead.StatusCode == HttpStatusCode.Forbidden, "US19 Intern foreign evaluation returns 403 without authentication scheme failure");

        await LeaveRequestChecks.RunAsync(http, connectionString, check, Login, Get);
        var repo = new DirectoryInfo(Path.GetDirectoryName(typeof(Sprint3OwnershipChecks).Assembly.Location)!);
        while (!Directory.Exists(Path.Combine(repo.FullName,"Frontend"))) repo = repo.Parent ?? throw new Exception("Repository not found.");
        var browserStart = new System.Diagnostics.ProcessStartInfo("python") {
            WorkingDirectory=repo.FullName,UseShellExecute=false,CreateNoWindow=true,WindowStyle=System.Diagnostics.ProcessWindowStyle.Hidden,
            RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true
        };
        browserStart.ArgumentList.Add(Path.Combine(repo.FullName,"tests","check_leave_review_live.py"));
        using(var browser=System.Diagnostics.Process.Start(browserStart) ?? throw new Exception("Could not start US24 Chrome check.")) {
            await browser.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { @base=http.BaseAddress!.ToString() }));browser.StandardInput.Close();
            var output=browser.StandardOutput.ReadToEndAsync();var errors=browser.StandardError.ReadToEndAsync();
            await browser.WaitForExitAsync();
            check(browser.ExitCode==0,"Chrome live US24 Mentor review: "+(browser.ExitCode==0?await output:await errors));
        }
        if(restart is not null) {
            await restart();await Login("ownership-hr");
            var persisted=await Get($"{evaluations}/{internA}");
            check(persisted.GetProperty("evaluationSource").GetString()=="HR" && persisted.GetProperty("mentorScore").GetDecimal()==8,"US19/20 HR and Mentor data persist after restart");
        }
    }
}
