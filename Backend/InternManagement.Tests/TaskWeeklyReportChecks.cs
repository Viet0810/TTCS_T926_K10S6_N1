using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InternManagement.Infrastructure;
using InternManagement.Services;
using Microsoft.Data.SqlClient;

internal static class TaskWeeklyReportChecks
{
    public static async Task RunAsync(HttpClient http, string connectionString, Action<bool,string> check, Func<Task> restart)
    {
        await using var sql = new SqlConnection(connectionString); await sql.OpenAsync();
        await using var command = sql.CreateCommand();
        command.Parameters.AddWithValue("@hash",new PasswordHasher().Hash("Progress-report-test-9"));
        command.CommandText = """
            INSERT dbo.Users(Username,FullName,Email,PasswordHash,Role) VALUES
                ('report-ma','Mentor A','report-ma@example.org',@hash,'MENTOR'),
                ('report-mb','Mentor B','report-mb@example.org',@hash,'MENTOR'),
                ('report-ia','Intern A','report-ia@example.org',@hash,'INTERN'),
                ('report-ib','Intern B','report-ib@example.org',@hash,'INTERN'),
                ('report-unmapped','Unmapped','report-unmapped@example.org',@hash,'INTERN'),
                ('report-hr','HR','report-hr@example.org',@hash,'HR');
            INSERT dbo.InternshipPrograms(Name,StartDate,EndDate) VALUES('Report program','2026-10-01','2026-12-31');
            INSERT dbo.Interns(FullName,Email,Phone,School,Major,MentorUserId,InternshipProgramId)
            SELECT 'Intern A','report-ia@example.org','0912345678','ICTU','IT',u.Id,p.Id
            FROM dbo.Users u CROSS JOIN dbo.InternshipPrograms p WHERE u.Username='report-ma' AND p.Name='Report program';
            INSERT dbo.Interns(FullName,Email,Phone,School,Major,MentorUserId)
            SELECT 'Intern B','report-ib@example.org','0912345678','ICTU','IT',Id FROM dbo.Users WHERE Username='report-mb';
            INSERT dbo.RolePermissions(Role,Permission)
            SELECT v.Role,v.Permission FROM (VALUES
                ('MENTOR','MENTOR_TASK_READ'),('MENTOR','TASK_CREATE'),
                ('MENTOR','MENTOR_WEEKLY_REPORT_READ'),('MENTOR','WEEKLY_REPORT_FEEDBACK'),
                ('INTERN','OWN_TASK_READ'),('INTERN','OWN_TASK_UPDATE'),
                ('INTERN','OWN_WEEKLY_REPORT_READ'),('INTERN','WEEKLY_REPORT_CREATE')
            ) v(Role,Permission)
            WHERE NOT EXISTS(SELECT 1 FROM dbo.RolePermissions r WHERE r.Role=v.Role AND r.Permission=v.Permission);
            """;
        await command.ExecuteNonQueryAsync(); command.Parameters.Clear();
        async Task<int> Scalar(string query) { command.CommandText=query; return Convert.ToInt32(await command.ExecuteScalarAsync()); }
        async Task Exec(string query) { command.CommandText=query; await command.ExecuteNonQueryAsync(); }
        var internA = await Scalar("SELECT Id FROM dbo.Interns WHERE Email='report-ia@example.org'");
        var internB = await Scalar("SELECT Id FROM dbo.Interns WHERE Email='report-ib@example.org'");
        var mentorB = await Scalar("SELECT Id FROM dbo.Users WHERE Username='report-mb'");
        var program = await Scalar("SELECT Id FROM dbo.InternshipPrograms WHERE Name='Report program'");
        async Task Login(string username)
        {
            http.DefaultRequestHeaders.Authorization=null;
            using var response=await http.PostAsJsonAsync("/api/auth/login",new { username,password="Progress-report-test-9" });
            check(response.StatusCode==HttpStatusCode.OK,"progress/report login "+username);
            using var body=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",body.RootElement.GetProperty("token").GetString());
        }
        async Task<JsonElement> Get(string path)
        {
            using var response=await http.GetAsync(path); check(response.StatusCode==HttpStatusCode.OK,"progress/report GET "+path);
            using var body=JsonDocument.Parse(await response.Content.ReadAsStringAsync()); return body.RootElement.Clone();
        }
        async Task Status(HttpResponseMessage response,HttpStatusCode expected,string name)
        {
            using(response) { check(response.StatusCode==expected,name+" (HTTP "+(int)response.StatusCode+")"); }
            await Task.CompletedTask;
        }
        const string reports="/api/weekly-reports";
        await Status(await http.GetAsync(reports),HttpStatusCode.Unauthorized,"US17 anonymous cannot read");
        await Login("report-ma");
        using var createTask=await http.PostAsJsonAsync("/api/tasks",new { internId=internA,content="Original mentor task",deadline=DateTimeOffset.UtcNow.AddDays(7) });
        check(createTask.StatusCode==HttpStatusCode.Created,"US16 reuses US15 task creation");
        using var taskBody=JsonDocument.Parse(await createTask.Content.ReadAsStringAsync());
        var taskId=taskBody.RootElement.GetProperty("id").GetInt32();
        // Simulate the old US15 schema in this disposable DB, then verify its in-place upgrade.
        await Exec("""
            ALTER TABLE dbo.InternTasks DROP CONSTRAINT CK_InternTasks_ProgressStatus,DF_InternTasks_Progress;
            ALTER TABLE dbo.InternTasks DROP COLUMN Progress,ProgressNote,UpdatedAt;
            ALTER TABLE dbo.InternTasks ADD CONSTRAINT CK_LegacyTaskStatus CHECK(Status IN ('ASSIGNED'));
            """);
        await InternTaskMigration.ApplyAsync(sql,default);
        check((await Get("/api/tasks/"+taskId)).GetProperty("progress").GetInt32()==0,"US16 migration preserves existing assigned task");
        await Status(await http.PatchAsJsonAsync($"/api/tasks/{taskId}/progress",new { progress=20 }),HttpStatusCode.Forbidden,"US16 Mentor cannot update Intern progress");
        await Login("report-ib");
        await Status(await http.PatchAsJsonAsync($"/api/tasks/{taskId}/progress",new { progress=20 }),HttpStatusCode.Forbidden,"US16 foreign Intern update returns 403");
        await Login("report-ia");
        await Status(await http.PatchAsJsonAsync($"/api/tasks/{taskId}/progress",new { progress=35,progressNote="Tiến độ tiếng Việt",content="Forged",deadline="2030-01-01T00:00:00Z",internId=internB,mentorUserId=mentorB,status="COMPLETED" }),HttpStatusCode.OK,"US16 owner updates progress");
        var task=await Get("/api/tasks/"+taskId);
        check(task.GetProperty("progress").GetInt32()==35 && task.GetProperty("status").GetString()=="IN_PROGRESS" && task.GetProperty("progressNote").GetString()=="Tiến độ tiếng Việt","US16 persisted progress/status/note");
        check(task.GetProperty("content").GetString()=="Original mentor task" && task.GetProperty("internId").GetInt32()==internA && task.GetProperty("mentorUserId").GetInt32()!=mentorB && task.GetProperty("deadline").GetDateTimeOffset().Year!=2030,"US16 forged Mentor-managed fields ignored");
        foreach(var value in new[] { 0,50,100 })
        {
            await Status(await http.PatchAsJsonAsync($"/api/tasks/{taskId}/progress",new { progress=value }),HttpStatusCode.OK,$"US16 owner progress {value}");
            var updated=await Get("/api/tasks/"+taskId);
            check(updated.GetProperty("progress").GetInt32()==value && updated.GetProperty("status").GetString()==(value==0?"ASSIGNED":value==100?"COMPLETED":"IN_PROGRESS"),"US16 reload reflects progress/status");
        }
        foreach(var value in new[] { -1,101 })
            await Status(await http.PatchAsJsonAsync($"/api/tasks/{taskId}/progress",new { progress=value }),HttpStatusCode.BadRequest,"US16 invalid percentage rejected");
        await Status(await http.PatchAsJsonAsync($"/api/tasks/{taskId}/progress",new { progressNote="Missing progress" }),HttpStatusCode.BadRequest,"US16 missing progress rejected");
        await Status(await http.PatchAsJsonAsync($"/api/tasks/{taskId}/progress",new { progress=40,progressNote=new string('x',2001) }),HttpStatusCode.BadRequest,"US16 oversized note rejected");
        await Exec("DELETE dbo.RolePermissions WHERE Role='INTERN' AND Permission='OWN_TASK_UPDATE'");
        await Status(await http.PatchAsJsonAsync($"/api/tasks/{taskId}/progress",new { progress=50 }),HttpStatusCode.Forbidden,"US16 revoked update permission returns 403");
        await Exec("INSERT dbo.RolePermissions(Role,Permission) VALUES('INTERN','OWN_TASK_UPDATE');DELETE dbo.RolePermissions WHERE Role='INTERN' AND Permission='OWN_TASK_READ'");
        await Status(await http.PatchAsJsonAsync($"/api/tasks/{taskId}/progress",new { progress=50 }),HttpStatusCode.Forbidden,"US16 update also requires own-task read permission");
        await Exec("INSERT dbo.RolePermissions(Role,Permission) VALUES('INTERN','OWN_TASK_READ')");
        await Status(await http.PatchAsJsonAsync($"/api/tasks/{taskId}/progress",new { progress=100 }),HttpStatusCode.OK,"US16 completion update succeeds");
        await Login("report-ma");
        check((await Get("/api/tasks/"+taskId)).GetProperty("status").GetString()=="COMPLETED","US16 Mentor sees updated completed status");

        await Login("report-unmapped");
        await Status(await http.PostAsJsonAsync(reports,new { weekStartDate="2026-10-13",content="Unmapped",internId=internA }),HttpStatusCode.Forbidden,"US17 unmapped account cannot impersonate Intern");
        await Login("report-ia");
        await Status(await http.PostAsJsonAsync(reports,new { content="Missing week" }),HttpStatusCode.BadRequest,"US17 week required");
        await Status(await http.PostAsJsonAsync(reports,new { weekStartDate="2026-10-13",content="  " }),HttpStatusCode.BadRequest,"US17 blank report rejected");
        await Status(await http.PostAsJsonAsync(reports,new { weekStartDate="2026-10-13",content=new string('x',4001) }),HttpStatusCode.BadRequest,"US17 oversized report rejected");
        using var created=await http.PostAsJsonAsync(reports,new { weekStartDate="2026-10-13",content="Báo cáo tuần tiếng Việt",internId=internB,submittedByUserId=9999,internshipProgramId=9999 });
        check(created.StatusCode==HttpStatusCode.Created,"US17 Intern submits weekly report");
        using var createdBody=JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var reportId=createdBody.RootElement.GetProperty("id").GetInt32();
        var own=await Get(reports+"/"+reportId);
        check(own.GetProperty("internId").GetInt32()==internA && own.GetProperty("internshipProgramId").GetInt32()==program
            && own.GetProperty("weekStartDate").GetString()=="2026-10-12" && own.GetProperty("weekEndDate").GetString()=="2026-10-18","US17 server maps owner/program and normalizes Monday-Sunday week");
        await Status(await http.PostAsJsonAsync(reports,new { weekStartDate="2026-10-15",content="Overwrite attempt" }),HttpStatusCode.Conflict,"US17 same-week duplicate cannot overwrite history");
        await Status(await http.PostAsJsonAsync(reports,new { weekStartDate="2026-10-19",content="Next weekly report" }),HttpStatusCode.Created,"US17 next week retains previous report");
        check((await Get(reports)).GetArrayLength()==2,"US17 own report history includes both weeks");
        await Login("report-ib");
        check((await Get(reports+"?internId="+internA)).GetArrayLength()==0,"US17 other Intern cannot list foreign history");
        await Status(await http.GetAsync(reports+"/"+reportId),HttpStatusCode.Forbidden,"US17 foreign report direct GET returns 403");
        await Status(await http.PostAsJsonAsync(reports+"/"+reportId+"/feedback",new { content="Forged feedback" }),HttpStatusCode.Forbidden,"US18 Intern cannot post feedback");
        await Login("report-mb");
        check((await Get(reports)).GetArrayLength()==0,"US18 other Mentor list excludes foreign reports");
        await Status(await http.GetAsync(reports+"/"+reportId),HttpStatusCode.Forbidden,"US18 other Mentor direct GET returns 403");
        await Status(await http.PostAsJsonAsync(reports+"/"+reportId+"/feedback",new { content="Wrong Mentor" }),HttpStatusCode.Forbidden,"US18 other Mentor feedback returns 403");
        await Login("report-ma");
        check((await Get(reports)).GetArrayLength()==2,"US18 current Mentor sees own Intern reports");
        await Status(await http.PostAsJsonAsync(reports,new { weekStartDate="2026-10-13",content="Mentor report" }),HttpStatusCode.Forbidden,"US17 Mentor cannot submit Intern report");
        await Status(await http.PostAsJsonAsync(reports+"/"+reportId+"/feedback",new { content="  " }),HttpStatusCode.BadRequest,"US18 blank feedback rejected");
        await Status(await http.PostAsJsonAsync(reports+"/"+reportId+"/feedback",new { content="Phản hồi tiếng Việt" }),HttpStatusCode.Created,"US18 assigned Mentor posts feedback");
        await Status(await http.PostAsJsonAsync(reports+"/"+reportId+"/feedback",new { content="Additional feedback" }),HttpStatusCode.Created,"US18 additional feedback retains earlier feedback");
        await Login("report-ia");
        own=await Get(reports+"/"+reportId);
        check(own.GetProperty("content").GetString()=="Báo cáo tuần tiếng Việt" && own.GetProperty("feedback").GetArrayLength()==2
            && own.GetProperty("feedback")[0].GetProperty("content").GetString()=="Phản hồi tiếng Việt","US18 Intern sees feedback history without overwriting report");
        await Exec("DELETE dbo.RolePermissions WHERE Role='INTERN' AND Permission='WEEKLY_REPORT_CREATE'");
        await Status(await http.PostAsJsonAsync(reports,new { weekStartDate="2026-10-26",content="No permission" }),HttpStatusCode.Forbidden,"US17 revoked create permission returns 403");
        await Exec("INSERT dbo.RolePermissions(Role,Permission) VALUES('INTERN','WEEKLY_REPORT_CREATE');DELETE dbo.RolePermissions WHERE Role='INTERN' AND Permission='OWN_WEEKLY_REPORT_READ'");
        await Status(await http.GetAsync(reports),HttpStatusCode.Forbidden,"US17 revoked read permission returns 403");
        await Status(await http.PostAsJsonAsync(reports,new { weekStartDate="2026-10-26",content="No read dependency" }),HttpStatusCode.Forbidden,"US17 create enforces read dependency");
        await Exec("INSERT dbo.RolePermissions(Role,Permission) VALUES('INTERN','OWN_WEEKLY_REPORT_READ')");
        await Login("report-ma");
        await Exec("DELETE dbo.RolePermissions WHERE Role='MENTOR' AND Permission='WEEKLY_REPORT_FEEDBACK'");
        await Status(await http.PostAsJsonAsync(reports+"/"+reportId+"/feedback",new { content="No permission" }),HttpStatusCode.Forbidden,"US18 revoked feedback permission returns 403");
        await Exec("INSERT dbo.RolePermissions(Role,Permission) VALUES('MENTOR','WEEKLY_REPORT_FEEDBACK');DELETE dbo.RolePermissions WHERE Role='MENTOR' AND Permission='MENTOR_WEEKLY_REPORT_READ'");
        await Status(await http.GetAsync(reports),HttpStatusCode.Forbidden,"US18 revoked read returns 403");
        await Status(await http.PostAsJsonAsync(reports+"/"+reportId+"/feedback",new { content="No read dependency" }),HttpStatusCode.Forbidden,"US18 feedback enforces read dependency");
        await Exec("INSERT dbo.RolePermissions(Role,Permission) VALUES('MENTOR','MENTOR_WEEKLY_REPORT_READ')");
        await Exec($"UPDATE dbo.Interns SET MentorUserId={mentorB},InternshipProgramId=NULL WHERE Id={internA}");
        await Status(await http.GetAsync(reports+"/"+reportId),HttpStatusCode.Forbidden,"US18 former Mentor loses read after reassignment");
        await Status(await http.PostAsJsonAsync(reports+"/"+reportId+"/feedback",new { content="Former Mentor" }),HttpStatusCode.Forbidden,"US18 former Mentor cannot add feedback");
        await Login("report-mb");
        check((await Get(reports+"/"+reportId)).GetProperty("internshipProgramId").GetInt32()==program,"US17 report retains original program after reassignment");
        await Status(await http.PostAsJsonAsync(reports+"/"+reportId+"/feedback",new { content="New Mentor feedback" }),HttpStatusCode.Created,"US18 new Mentor uses current US30 assignment");
        await Exec("DELETE dbo.RolePermissions WHERE Role='MENTOR' AND Permission='WEEKLY_REPORT_FEEDBACK';DELETE dbo.RolePermissions WHERE Role='INTERN' AND Permission='OWN_TASK_UPDATE'");
        await restart();
        await Login("report-mb");
        await Status(await http.PostAsJsonAsync(reports+"/"+reportId+"/feedback",new { content="Still revoked" }),HttpStatusCode.Forbidden,"US18 restart preserves revoked feedback grant");
        await Login("report-ia");
        own=await Get(reports+"/"+reportId);
        check(own.GetProperty("feedback").GetArrayLength()==3 && (await Get(reports)).GetArrayLength()==2,"US17/18 report and feedback persist after backend restart");
        check((await Get("/api/tasks/"+taskId)).GetProperty("progress").GetInt32()==100,"US16 progress persists after backend restart");
        await Status(await http.PatchAsJsonAsync($"/api/tasks/{taskId}/progress",new { progress=50 }),HttpStatusCode.Forbidden,"US16 restart preserves revoked update grant");
        check(await Scalar("SELECT COUNT(*) FROM dbo.WeeklyReports WHERE Content='Overwrite attempt'")==0,"US17 rejected submissions create no rows");
        check(await Scalar("SELECT COUNT(*) FROM dbo.WeeklyReportFeedbacks")==3,"US18 only authorized feedback persisted");
        check(await Scalar("SELECT COUNT(*) FROM dbo.Notifications WHERE Type='TASK_ASSIGNED'")==1,"US36 one real task notification");
        check(await Scalar("SELECT COUNT(*) FROM dbo.Notifications WHERE Type='REPORT_FEEDBACK'")==3,"US36 only successful feedback triggers notifications");
        check(await Scalar("SELECT COUNT(*) FROM dbo.Notifications n JOIN dbo.Users u ON n.UserId=u.Id WHERE n.Type IN ('TASK_ASSIGNED','REPORT_FEEDBACK') AND u.Username<>'report-ia'")==0,"US36 no notifications to outsiders");
        await Login("report-hr");
        await Status(await http.GetAsync(reports),HttpStatusCode.Forbidden,"US17/18 HR has no implicit report access");
        await Exec($"UPDATE dbo.Interns SET MentorUserId=(SELECT Id FROM dbo.Users WHERE Username='report-ma') WHERE Id={internA}; UPDATE dbo.InternTasks SET Progress=0,Status='ASSIGNED' WHERE Id={taskId}; INSERT dbo.RolePermissions(Role,Permission) VALUES('INTERN','OWN_TASK_UPDATE'),('MENTOR','WEEKLY_REPORT_FEEDBACK')");
        await WorkScheduleChecks.RunAsync(http,connectionString,check);
        var repo = new DirectoryInfo(Path.GetDirectoryName(typeof(TaskWeeklyReportChecks).Assembly.Location)!);
        while (!Directory.Exists(Path.Combine(repo.FullName,"Frontend"))) repo = repo.Parent ?? throw new Exception("Repository not found.");
        var browserStart = new System.Diagnostics.ProcessStartInfo("python") {
            WorkingDirectory=repo.FullName,UseShellExecute=false,CreateNoWindow=true,WindowStyle=System.Diagnostics.ProcessWindowStyle.Hidden,
            RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true
        };
        browserStart.ArgumentList.Add(Path.Combine(repo.FullName,"tests","check_reports_live.py"));
        using var browser=System.Diagnostics.Process.Start(browserStart) ?? throw new Exception("Could not start live RBAC UI check.");
        await browser.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { @base=http.BaseAddress!.ToString() }));browser.StandardInput.Close();
        var output=browser.StandardOutput.ReadToEndAsync();var errors=browser.StandardError.ReadToEndAsync();
        await browser.WaitForExitAsync();
        check(browser.ExitCode==0,"Chrome live US16-US18: "+(browser.ExitCode==0?"PASS":await errors));
        await restart();await Login("report-ia");
        check((await Get("/api/work-schedules")).GetArrayLength()==3,"US23 work schedules persist after backend restart");
        check((await Get("/api/weekly-reports")).EnumerateArray().Any(r=>r.GetProperty("workDone").ValueKind==JsonValueKind.String),"US17 structured sections persist after restart");
    }
}
