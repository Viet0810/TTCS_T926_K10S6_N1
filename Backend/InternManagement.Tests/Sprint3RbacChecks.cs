using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InternManagement.Services;
using Microsoft.Data.SqlClient;

internal static class Sprint3RbacChecks
{
    public static async Task RunAsync(HttpClient http,string connectionString,Action<bool,string> check,Func<Task> restart)
    {
        await using var sql=new SqlConnection(connectionString);await sql.OpenAsync();await using var q=sql.CreateCommand();
        q.Parameters.AddWithValue("@hash",new PasswordHasher().Hash("Sprint3-rbac-test-9"));
        q.CommandText="""
            INSERT dbo.Users(Username,FullName,Email,PasswordHash,Role) VALUES
                ('s3-admin','Admin','s3-admin@example.org',@hash,'ADMIN'),('s3-hr','HR','s3-hr@example.org',@hash,'HR'),
                ('s3-ma','Mentor A','s3-ma@example.org',@hash,'MENTOR'),('s3-mb','Mentor B','s3-mb@example.org',@hash,'MENTOR'),
                ('s3-ia','Intern A','s3-ia@example.org',@hash,'INTERN'),('s3-ib','Intern B','s3-ib@example.org',@hash,'INTERN');
            INSERT dbo.InternshipPrograms(Name,StartDate,EndDate) VALUES('S3 RBAC','2026-01-01','2030-12-31');
            INSERT dbo.Interns(FullName,Email,Phone,School,Major,MentorUserId,InternshipProgramId)
            SELECT 'Intern A','s3-ia@example.org','0912345678','ICTU','IT',u.Id,p.Id
            FROM dbo.Users u CROSS JOIN dbo.InternshipPrograms p WHERE u.Username='s3-ma' AND p.Name='S3 RBAC';
            INSERT dbo.Interns(FullName,Email,Phone,School,Major,MentorUserId)
            SELECT 'Intern B','s3-ib@example.org','0912345678','ICTU','IT',Id FROM dbo.Users WHERE Username='s3-mb';
            INSERT dbo.Interns(FullName,Email,Phone,School,Major)
            VALUES('Unassigned','s3-unassigned@example.org','0912345678','ICTU','IT');
            INSERT dbo.InternContracts(InternId,FileName,Content)
            SELECT Id,'contract.pdf',0x255044462D FROM dbo.Interns WHERE Email='s3-unassigned@example.org';
            INSERT dbo.InternDocuments(InternId,Kind,FileName,Content,ReviewStatus)
            SELECT Id,'application','application.pdf',0x255044462D,'approved' FROM dbo.Interns WHERE Email='s3-unassigned@example.org';
            """;
        await q.ExecuteNonQueryAsync();q.Parameters.Clear();
        async Task<int> Id(string table,string column,string value)
        {q.CommandText=$"SELECT Id FROM dbo.{table} WHERE {column}=@value";q.Parameters.Clear();q.Parameters.AddWithValue("@value",value);return Convert.ToInt32(await q.ExecuteScalarAsync());}
        var ia=await Id("Interns","Email","s3-ia@example.org");var ib=await Id("Interns","Email","s3-ib@example.org");
        var unassigned=await Id("Interns","Email","s3-unassigned@example.org");var ma=await Id("Users","Username","s3-ma");
        var iaUser=await Id("Users","Username","s3-ia");var program=await Id("InternshipPrograms","Name","S3 RBAC");
        q.Parameters.Clear();
        q.CommandText="SELECT Role,Permission FROM dbo.RolePermissions WHERE Role<>'ADMIN' ORDER BY Role,Permission";
        var otherRoles=new List<string>();await using(var reader=await q.ExecuteReaderAsync())while(await reader.ReadAsync())otherRoles.Add(reader.GetString(0)+":"+reader.GetString(1));
        q.CommandText="DELETE dbo.PermissionMigrations WHERE Name='Sprint3AdminCatalogRepair'; DELETE dbo.RolePermissions WHERE Role='ADMIN' AND Permission IN ('MANAGE_USERS','RBAC_READ','MANAGE_PERMISSIONS','OWN_TASK_UPDATE')";
        await q.ExecuteNonQueryAsync();
        await InternManagement.Infrastructure.Sprint3PermissionMigration.ApplyAsync(sql,default);
        var repaired=new RolePermissionService(new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"ConnectionStrings:InternManagement",connectionString}}).Build());
        check(PermissionCatalog.Entries.All(p=>repaired.GetPermissions("ADMIN").Contains(p.Code)),"ADMIN repair restores full catalog on previously migrated DB");
        await InternManagement.Infrastructure.Sprint3PermissionMigration.ApplyAsync(sql,default);
        q.CommandText="SELECT Role,Permission FROM dbo.RolePermissions WHERE Role<>'ADMIN' ORDER BY Role,Permission";
        var afterRoles=new List<string>();await using(var reader=await q.ExecuteReaderAsync())while(await reader.ReadAsync())afterRoles.Add(reader.GetString(0)+":"+reader.GetString(1));
        check(otherRoles.SequenceEqual(afterRoles),"ADMIN repair leaves HR/Mentor/Intern byte-for-byte grants unchanged");
        q.CommandText="DELETE dbo.RolePermissions WHERE Role='ADMIN' AND Permission='TASK_CREATE'";await q.ExecuteNonQueryAsync();
        await InternManagement.Infrastructure.Sprint3PermissionMigration.ApplyAsync(sql,default);
        check(!repaired.GetPermissions("ADMIN").Contains("TASK_CREATE"),"ADMIN repair is idempotent and preserves later optional revocation");
        q.CommandText="INSERT dbo.RolePermissions(Role,Permission) VALUES('ADMIN','TASK_CREATE')";await q.ExecuteNonQueryAsync();
        async Task<string> Token(string name)
        {
            http.DefaultRequestHeaders.Authorization=null;
            using var response=await http.PostAsJsonAsync("/api/auth/login",new {username=name,password="Sprint3-rbac-test-9"});
            check(response.StatusCode==HttpStatusCode.OK,"S3 RBAC login "+name);
            using var body=JsonDocument.Parse(await response.Content.ReadAsStringAsync());return body.RootElement.GetProperty("token").GetString()!;
        }
        var admin=await Token("s3-admin");var hr=await Token("s3-hr");var mentor=await Token("s3-ma");var otherMentor=await Token("s3-mb");var intern=await Token("s3-ia");var otherIntern=await Token("s3-ib");
        void As(string token)=>http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);
        async Task<JsonElement> Json(HttpResponseMessage response)
        {using(response){check(response.IsSuccessStatusCode,"S3 RBAC successful HTTP "+(int)response.StatusCode);using var body=JsonDocument.Parse(await response.Content.ReadAsStringAsync());return body.RootElement.Clone();}}
        void Status(HttpResponseMessage response,HttpStatusCode expected,string name)
        {using(response)check(response.StatusCode==expected,name+" (HTTP "+(int)response.StatusCode+")");}
        async Task Set(string role,string code,bool enabled)
        {
            As(admin);var saved=await Json(await http.GetAsync("/api/permissions/"+role));
            var grants=saved.GetProperty("permissions").EnumerateArray().Select(x=>x.GetString()!).ToHashSet();
            if(enabled)grants.Add(code);else grants.Remove(code);
            Status(await http.PutAsJsonAsync("/api/permissions/"+role,new {permissions=grants.ToArray()}),HttpStatusCode.OK,$"Admin {(enabled?"grants":"revokes")} {role}/{code}");
            var refetched=await Json(await http.GetAsync("/api/permissions/"+role));
            check(refetched.GetProperty("permissions").EnumerateArray().Any(x=>x.GetString()==code)==enabled,"permission refetch reflects persisted DB grant");
        }
        if(Environment.GetCommandLineArgs().Contains("--admin-rbac-only")) {
            await restart();As(admin);
            var adminVisible=await Json(await http.GetAsync("/api/users"));
            check(adminVisible.EnumerateArray().All(u=>u.GetProperty("role").GetString()!="ADMIN"),"ADMIN business account list excludes ADMIN accounts");
            var adminPaged=await Json(await http.GetAsync("/api/users/page?page=1&pageSize=100"));
            check(adminPaged.GetProperty("items").EnumerateArray().All(u=>u.GetProperty("role").GetString()!="ADMIN"),"ADMIN paged account list excludes ADMIN accounts");
            Status(await http.GetAsync("/api/permissions"),HttpStatusCode.OK,"ADMIN RBAC API accessible after restart");
            check(PermissionCatalog.Entries.All(p=>repaired.GetPermissions("ADMIN").Contains(p.Code)),"ADMIN full catalog persists after restart");
            q.Parameters.Clear();q.CommandText="SELECT Role,Permission FROM dbo.RolePermissions WHERE Role<>'ADMIN' ORDER BY Role,Permission";
            var rolesAfterRestart=new List<string>();await using(var r=await q.ExecuteReaderAsync())while(await r.ReadAsync())rolesAfterRestart.Add(r.GetString(0)+":"+r.GetString(1));
            check(otherRoles.SequenceEqual(rolesAfterRestart),"Other roles unchanged after restart");
            var mentorOriginal=repaired.GetPermissions("MENTOR").ToArray();
            await Set("MENTOR","MANAGE_USERS",true);await Set("MENTOR","CREATE_USER",true);await Set("MENTOR","DELETE_USER",true);
            As(mentor);
            var visible=await Json(await http.GetAsync("/api/users"));
            check(visible.EnumerateArray().All(u=>u.GetProperty("role").GetString()!="ADMIN"),"Non-Admin cannot list ADMIN accounts despite account permission");
            var adminId=await Id("Users","Username","s3-admin");
            Status(await http.GetAsync("/api/users/"+adminId),HttpStatusCode.Forbidden,"Non-Admin direct ADMIN lookup returns 403");
            Status(await http.DeleteAsync("/api/users/"+adminId),HttpStatusCode.Forbidden,"Non-Admin cannot delete ADMIN account");
            Status(await http.PostAsJsonAsync("/api/users",new{fullName="Forbidden Admin",email="forbidden-admin@example.org",role="ADMIN"}),HttpStatusCode.Forbidden,"Non-Admin cannot create ADMIN account");
            As(admin);Status(await http.DeleteAsync("/api/users/"+adminId),HttpStatusCode.Forbidden,"ADMIN account cannot be deleted through account management API");
            Status(await http.PutAsJsonAsync("/api/permissions/MENTOR",new{permissions=mentorOriginal}),HttpStatusCode.OK,"Restore Mentor grants exactly after account visibility tests");

        var adminRepo = new DirectoryInfo(Path.GetDirectoryName(typeof(Sprint3RbacChecks).Assembly.Location)!);
        while (!Directory.Exists(Path.Combine(adminRepo.FullName,"Frontend"))) adminRepo = adminRepo.Parent ?? throw new Exception("Repository not found.");
        var adminBrowserStart = new System.Diagnostics.ProcessStartInfo("python") {
            WorkingDirectory=adminRepo.FullName,UseShellExecute=false,CreateNoWindow=true,WindowStyle=System.Diagnostics.ProcessWindowStyle.Hidden,
            RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true
        };
        adminBrowserStart.ArgumentList.Add(Path.Combine(adminRepo.FullName,"tests","check_admin_rbac_live.py"));
        using var adminBrowser=System.Diagnostics.Process.Start(adminBrowserStart) ?? throw new Exception("Could not start live RBAC UI check.");
        await adminBrowser.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { @base=http.BaseAddress!.ToString() }));adminBrowser.StandardInput.Close();
        var adminOutput=adminBrowser.StandardOutput.ReadToEndAsync();var adminErrors=adminBrowser.StandardError.ReadToEndAsync();
        await adminBrowser.WaitForExitAsync();
        check(adminBrowser.ExitCode==0,"Chrome ADMIN menu/account/RBAC: "+(adminBrowser.ExitCode==0?"PASS":await adminErrors));
            return;
        }
        As(admin);var matrix=await Json(await http.GetAsync("/api/permissions"));
        check(matrix.GetProperty("catalog").GetArrayLength()==PermissionCatalog.Entries.Length,"S3 catalog definitions persisted and exposed to matrix");
        foreach(var role in new[]{"ADMIN","HR","MENTOR","INTERN"})
        {
            var defaults=await Json(await http.GetAsync($"/api/permissions/{role}/defaults"));
            check(defaults.GetProperty("permissions").EnumerateArray().All(p=>matrix.GetProperty("roles").GetProperty(role).EnumerateArray().Any(x=>x.GetString()==p.GetString())),"fresh DB defaults for "+role);
        }
        Status(await http.PutAsJsonAsync("/api/permissions/ADMIN",new {permissions=Array.Empty<string>()}),HttpStatusCode.BadRequest,"Admin cannot remove essential rights or lock itself out");
        Status(await http.PutAsJsonAsync("/api/permissions/INTERN",new {permissions=new[]{"OWN_TASK_UPDATE"}}),HttpStatusCode.BadRequest,"API rejects action without read dependency");
        Status(await http.PutAsJsonAsync("/api/permissions/MENTOR",new {permissions=new[]{"MEETING_READ","MEETING_CREATE"}}),HttpStatusCode.BadRequest,"API rejects business permission ineligible for role");

        As(mentor);var task=await Json(await http.PostAsJsonAsync("/api/tasks",new {internId=ia,content="S3 permission task",deadline=DateTimeOffset.UtcNow.AddDays(5)}));var taskId=task.GetProperty("id").GetInt32();
        await Set("MENTOR","TASK_CREATE",false);As(mentor);
        Status(await http.PostAsJsonAsync("/api/tasks",new {internId=ia,content="Denied",deadline=DateTimeOffset.UtcNow.AddDays(5)}),HttpStatusCode.Forbidden,"US15 revoked TASK_CREATE direct API 403");
        await Set("MENTOR","TASK_CREATE",true);As(mentor);
        Status(await http.PostAsJsonAsync("/api/tasks",new {internId=ia,content="Allowed again",deadline=DateTimeOffset.UtcNow.AddDays(5)}),HttpStatusCode.Created,"US15 regrant restores assignment");
        await Set("INTERN","OWN_TASK_UPDATE",false);As(intern);
        Status(await http.PatchAsJsonAsync($"/api/tasks/{taskId}/progress",new {progress=50}),HttpStatusCode.Forbidden,"US16 revoked progress API 403");
        await Set("INTERN","OWN_TASK_UPDATE",true);As(intern);
        Status(await http.PatchAsJsonAsync($"/api/tasks/{taskId}/progress",new {progress=50}),HttpStatusCode.OK,"US16 regrant restores progress update");
        As(otherIntern);Status(await http.PatchAsJsonAsync($"/api/tasks/{taskId}/progress",new {progress=99}),HttpStatusCode.Forbidden,"US16 permission cannot bypass ownership");
        As(intern);var report=await Json(await http.PostAsJsonAsync("/api/weekly-reports",new {weekStartDate="2026-10-12",content="S3 report"}));var reportId=report.GetProperty("id").GetInt32();
        await Set("MENTOR","WEEKLY_REPORT_FEEDBACK",false);As(mentor);
        Status(await http.PostAsJsonAsync($"/api/weekly-reports/{reportId}/feedback",new {content="Denied"}),HttpStatusCode.Forbidden,"US18 revoked feedback API 403");
        await Set("MENTOR","WEEKLY_REPORT_FEEDBACK",true);As(mentor);
        Status(await http.PostAsJsonAsync($"/api/weekly-reports/{reportId}/feedback",new {content="Allowed again"}),HttpStatusCode.Created,"US18 regrant restores feedback");
        As(otherMentor);Status(await http.GetAsync($"/api/weekly-reports/{reportId}"),HttpStatusCode.Forbidden,"US18 read permission cannot bypass current assignment");
        var evaluation=new{period="S3",attendance=8,attitude=8,professionalSkills=8,workResult=8,finalScore=8,classification="Good"};
        As(mentor);Status(await http.PostAsJsonAsync("/api/FinalEvaluations/"+ib,evaluation),HttpStatusCode.Forbidden,"US19 evaluation permission cannot bypass Mentor relationship");
        Status(await http.PostAsJsonAsync("/api/FinalEvaluations/"+ia,evaluation),HttpStatusCode.Created,"US19 assigned Mentor still evaluates");
        await Set("MENTOR","UPDATE_FINAL_EVALUATIONS",false);As(mentor);
        Status(await http.PutAsJsonAsync("/api/FinalEvaluations/"+ia,evaluation),HttpStatusCode.Forbidden,"US19 revoked evaluation update returns 403");
        await Set("MENTOR","UPDATE_FINAL_EVALUATIONS",true);
        await Set("HR","UPDATE_FINAL_EVALUATIONS",false);await Set("HR","VIEW_FINAL_EVALUATIONS",false);As(hr);
        Status(await http.GetAsync("/api/FinalEvaluations"),HttpStatusCode.Forbidden,"US20 HR revoked read returns 403");
        await Set("HR","VIEW_FINAL_EVALUATIONS",true);await Set("HR","UPDATE_FINAL_EVALUATIONS",true);
        await Set("HR","ASSIGNMENT_CREATE",false);As(hr);
        Status(await http.PutAsJsonAsync("/api/intern-assignments/"+unassigned,new{mentorUserId=ma,internshipProgramId=program}),HttpStatusCode.Forbidden,"US30 missing create grant cannot use update grant to assign new Intern");
        await Set("HR","ASSIGNMENT_CREATE",true);As(hr);
        Status(await http.PutAsJsonAsync("/api/intern-assignments/"+unassigned,new{mentorUserId=ma,internshipProgramId=program}),HttpStatusCode.OK,"US30 restored create grant assigns Mentor");
        Status(await http.PutAsJsonAsync("/api/intern-assignments/"+unassigned,new{mentorUserId=ma,internshipProgramId=program}),HttpStatusCode.OK,"US30 duplicate assignment is idempotent");
        Status(await http.PutAsJsonAsync("/api/intern-assignments/"+unassigned,new{mentorUserId=iaUser,internshipProgramId=program}),HttpStatusCode.BadRequest,"US30 rejects non-Mentor user");
        q.Parameters.Clear();q.CommandText=$"INSERT dbo.AttendanceRecords(InternId,Date,CheckIn,Status) VALUES({unassigned},'2026-10-14','08:00','ON_TIME')";
        await q.ExecuteNonQueryAsync();
        var mb=await Id("Users","Username","s3-mb");
        Status(await http.PutAsJsonAsync("/api/intern-assignments/"+unassigned,new{mentorUserId=mb}),HttpStatusCode.OK,"US30 reassign without resetting program");
        q.Parameters.Clear();q.CommandText=$"SELECT COUNT(*) FROM dbo.Interns i WHERE i.Id={unassigned} AND i.InternshipProgramId={program} AND EXISTS(SELECT 1 FROM dbo.InternContracts WHERE InternId=i.Id) AND EXISTS(SELECT 1 FROM dbo.AttendanceRecords WHERE InternId=i.Id AND CheckIn='08:00')";
        check(Convert.ToInt32(await q.ExecuteScalarAsync())==1,"US30 reassignment retains program/contract/attendance");
        var afterMove=await Json(await http.GetAsync("/api/intern-assignments/mentor-load"));
        check(afterMove.EnumerateArray().Single(x=>x.GetProperty("mentorId").GetInt32()==mb).GetProperty("internCount").GetInt32()==2,"US31 count follows reassignment");
        Status(await http.PutAsJsonAsync("/api/intern-assignments/"+unassigned,new{mentorUserId=ma}),HttpStatusCode.OK,"US30 restore test assignment");
        await Set("HR","MENTOR_WORKLOAD_READ",false);As(hr);
        Status(await http.GetAsync("/api/intern-assignments/mentor-load"),HttpStatusCode.Forbidden,"US31 workload can be revoked independently");
        Status(await http.GetAsync("/api/intern-assignments"),HttpStatusCode.OK,"US31 revocation does not break assignment read");
        await Set("HR","MENTOR_WORKLOAD_READ",true);As(hr);
        Status(await http.GetAsync("/api/intern-assignments/mentor-load"),HttpStatusCode.OK,"US31 workload regrant works");
        await Set("HR","MEETING_CREATE",false);As(hr);
        var meeting=new{title="S3 RBAC meeting",startTime=DateTimeOffset.UtcNow.AddDays(3),endTime=DateTimeOffset.UtcNow.AddDays(3).AddHours(1),participantUserIds=new[]{iaUser,ma},clientRequestId=Guid.NewGuid()};
        Status(await http.PostAsJsonAsync("/api/meetings",meeting),HttpStatusCode.Forbidden,"US35 revoked MEETING_CREATE API 403");
        await Set("HR","MEETING_CREATE",true);As(hr);
        Status(await http.PostAsJsonAsync("/api/meetings",meeting),HttpStatusCode.Created,"US35 regrant creates meeting safely with SMTP disabled");
        As(intern);var ownMeetings=await Json(await http.GetAsync("/api/meetings"));check(ownMeetings.GetArrayLength()==1,"US35 selected Intern sees related meeting");
        Status(await http.GetAsync("/api/meetings/participants"),HttpStatusCode.Forbidden,"US35 read-only participant cannot browse user directory");
        As(otherIntern);Status(await http.GetAsync("/api/meetings/"+ownMeetings[0].GetProperty("id").GetInt32()),HttpStatusCode.Forbidden,"US35 outsider direct meeting read 403");
        As(otherIntern);check((await Json(await http.GetAsync("/api/meetings"))).GetArrayLength()==0,"US35 nonparticipant cannot see meeting despite read grant");
        As(mentor);check((await Json(await http.GetAsync("/api/meetings"))).GetArrayLength()==1,"US35 selected Mentor sees related meeting");
        await Set("INTERN","LEAVE_REQUEST_CREATE",false);As(intern);
        var leave=new{leaveType="OTHER",durationType="FULL_DAY",startDate="2026-10-22",endDate="2026-10-22",reason="S3 permission integration test"};
        Status(await http.PostAsJsonAsync("/api/interns/me/leave-requests",leave),HttpStatusCode.Forbidden,"US24 revoked leave-create returns 403");
        await Set("INTERN","LEAVE_REQUEST_CREATE",true);As(intern);
        var leaveData=await Json(await http.PostAsJsonAsync("/api/interns/me/leave-requests",leave));var leaveId=leaveData.GetProperty("id").GetInt64();
        await Set("INTERN","LEAVE_REQUEST_CANCEL",false);As(intern);
        Status(await http.PostAsync($"/api/interns/me/leave-requests/{leaveId}/cancel",null),HttpStatusCode.Forbidden,"US24 revoked cancellation returns 403");
        await Set("INTERN","LEAVE_REQUEST_CANCEL",true);As(otherIntern);
        Status(await http.PostAsync($"/api/interns/me/leave-requests/{leaveId}/cancel",null),HttpStatusCode.Forbidden,"US24 cancellation grant cannot bypass ownership");
        As(intern);Status(await http.PostAsync($"/api/interns/me/leave-requests/{leaveId}/cancel",null),HttpStatusCode.OK,"US24 owner cancels after regrant");
        As(admin);var notification=await Json(await http.PostAsJsonAsync("/api/notifications",new{userId=iaUser,title="S3 notice",message="Owner only",category="SYSTEM",type="S3_TEST"}));var notificationId=notification.GetProperty("id").GetInt64();
        await Set("INTERN","NOTIFICATION_UPDATE",false);await Set("INTERN","NOTIFICATION_READ",false);As(intern);
        Status(await http.GetAsync("/api/notifications"),HttpStatusCode.Forbidden,"US36 revoked notification read direct API 403");
        Status(await http.GetAsync("/api/notifications/unread-count"),HttpStatusCode.Forbidden,"US36 unread count also protected");
        await Set("INTERN","NOTIFICATION_READ",true);As(intern);
        Status(await http.PostAsync($"/api/notifications/{notificationId}/read",null),HttpStatusCode.Forbidden,"US36 read permission does not allow marking read");
        await Set("INTERN","NOTIFICATION_UPDATE",true);As(otherIntern);
        Status(await http.PostAsync($"/api/notifications/{notificationId}/read",null),HttpStatusCode.Forbidden,"US36 update permission cannot bypass notification owner");
        As(intern);Status(await http.PostAsync($"/api/notifications/{notificationId}/read",null),HttpStatusCode.OK,"US36 owner marks read after regrant");
        As(hr);var users=await Json(await http.GetAsync("/api/users"));check(users.EnumerateArray().All(u=>u.GetProperty("role").GetString()=="MENTOR"),"US29 HR account read scoped to Mentors");
        Status(await http.PostAsJsonAsync("/api/users",new{fullName="Forbidden HR",email="s3-forbidden@example.org",role="HR"}),HttpStatusCode.Forbidden,"US29 HR cannot use Mentor management for another HR account");
        Status(await http.PostAsJsonAsync("/api/users",new{fullName="Added Mentor",email="s3-added-mentor@example.org",role="MENTOR"}),HttpStatusCode.Created,"US29 HR default creates Mentor through reused account API");
        Status(await http.PostAsJsonAsync("/api/users",new{fullName="Duplicate",email="s3-added-mentor@example.org",role="MENTOR"}),HttpStatusCode.Conflict,"US29 duplicate Mentor rejected");
        q.Parameters.Clear();q.CommandText="SELECT COUNT(*) FROM dbo.Users WHERE Email='s3-added-mentor@example.org' AND Role='MENTOR' AND MustChangePassword=1";
        check(Convert.ToInt32(await q.ExecuteScalarAsync())==1,"US29 persisted Mentor requires password change");
        var loads=await Json(await http.GetAsync("/api/intern-assignments/mentor-load"));
        check(loads.EnumerateArray().Any(x=>x.GetProperty("internCount").GetInt32()==0)&&loads.EnumerateArray().Any(x=>x.GetProperty("internCount").GetInt32()==1)&&loads.EnumerateArray().Any(x=>x.GetProperty("internCount").GetInt32()>1),"US31 workload includes 0/1/multiple Interns");
        await Set("HR","CREATE_USER",false);As(hr);
        Status(await http.PostAsJsonAsync("/api/users",new{fullName="Denied Mentor",email="s3-denied-mentor@example.org",role="MENTOR"}),HttpStatusCode.Forbidden,"US29 revoked Mentor create returns 403");
        await Set("HR","CREATE_USER",true);
        await Set("MENTOR","TASK_CREATE",false);await Set("HR","MEETING_CREATE",false);
        await restart();As(mentor);
        Status(await http.PostAsJsonAsync("/api/tasks",new{internId=ia,content="Still denied",deadline=DateTimeOffset.UtcNow.AddDays(5)}),HttpStatusCode.Forbidden,"US15 revocation survives backend restart");
        As(hr);Status(await http.PostAsJsonAsync("/api/meetings",meeting),HttpStatusCode.Forbidden,"US35 revocation survives backend restart");
        As(admin);var after=await Json(await http.GetAsync("/api/permissions"));
        check(!after.GetProperty("roles").GetProperty("MENTOR").EnumerateArray().Any(x=>x.GetString()=="TASK_CREATE")
            && after.GetProperty("roles").GetProperty("INTERN").EnumerateArray().Any(x=>x.GetString()=="NOTIFICATION_UPDATE"),"RBAC matrix refetch retains revoked and restored grants after restart");
        Status(await http.PutAsJsonAsync("/api/permissions/ADMIN",new{permissions=Array.Empty<string>()}),HttpStatusCode.BadRequest,"Admin protection survives restart");
        await Set("MENTOR","TASK_CREATE",true);await Set("HR","MEETING_CREATE",true);
        var repo = new DirectoryInfo(Path.GetDirectoryName(typeof(Sprint3RbacChecks).Assembly.Location)!);
        while (!Directory.Exists(Path.Combine(repo.FullName,"Frontend"))) repo = repo.Parent ?? throw new Exception("Repository not found.");
        var browserStart = new System.Diagnostics.ProcessStartInfo("python") {
            WorkingDirectory=repo.FullName,UseShellExecute=false,CreateNoWindow=true,WindowStyle=System.Diagnostics.ProcessWindowStyle.Hidden,
            RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true
        };
        browserStart.ArgumentList.Add(Path.Combine(repo.FullName,"tests","check_sprint3_rbac_live.py"));
        using var browser=System.Diagnostics.Process.Start(browserStart) ?? throw new Exception("Could not start live RBAC UI check.");
        await browser.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { @base=http.BaseAddress!.ToString() }));browser.StandardInput.Close();
        var output=browser.StandardOutput.ReadToEndAsync();var errors=browser.StandardError.ReadToEndAsync();
        await browser.WaitForExitAsync();
        check(browser.ExitCode==0,"Chrome real RBAC matrix checkbox/save/refetch matches SQL: "+(browser.ExitCode==0?"PASS":await errors));
        check((await output).Contains("PASS: live matrix DB load/save/refetch"),"live browser exercised Sprint 3 permission controls");
    }
}
