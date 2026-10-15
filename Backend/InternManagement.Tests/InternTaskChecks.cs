using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InternManagement.Services;
using Microsoft.Data.SqlClient;

internal static class InternTaskChecks
{
    public static async Task RunAsync(HttpClient http, string connectionString, Action<bool,string> check, Func<Task> restart)
    {
        await using var sql = new SqlConnection(connectionString); await sql.OpenAsync();
        await using var command = sql.CreateCommand();
        command.Parameters.AddWithValue("@hash", new PasswordHasher().Hash("Task-test-9"));
        command.CommandText = """
            INSERT dbo.Users(Username,FullName,Email,PasswordHash,Role) VALUES
                ('task-mentor-a','Mentor A','mentor-a@company.vn',@hash,'MENTOR'),
                ('task-mentor-b','Mentor B','mentor-b@company.vn',@hash,'MENTOR'),
                ('task-intern-a','Intern A','intern-a@company.vn',@hash,'INTERN'),
                ('task-intern-b','Intern B','intern-b@company.vn',@hash,'INTERN'),
                ('task-hr','HR','hr@company.vn',@hash,'HR');
            INSERT dbo.Interns(FullName,Email,Phone,School,Major,MentorUserId)
            SELECT 'Intern A','intern-a@company.vn','0912345678','ICTU','IT',Id FROM dbo.Users WHERE Username='task-mentor-a';
            INSERT dbo.Interns(FullName,Email,Phone,School,Major,MentorUserId)
            SELECT 'Intern B','intern-b@company.vn','0912345678','ICTU','IT',Id FROM dbo.Users WHERE Username='task-mentor-b';
            SELECT Id FROM dbo.Interns ORDER BY Email;
            """;
        var ids = new List<int>();
        await using(var reader = await command.ExecuteReaderAsync()) while(await reader.ReadAsync()) ids.Add(reader.GetInt32(0));
        command.Parameters.Clear();
        async Task Login(string username)
        {
            http.DefaultRequestHeaders.Authorization = null;
            using var response = await http.PostAsJsonAsync("/api/auth/login", new { username, password="Task-test-9" });
            check(response.IsSuccessStatusCode, "task login " + username);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.RootElement.GetProperty("token").GetString());
        }
        async Task<JsonElement> Get(string path)
        {
            using var response = await http.GetAsync(path); check(response.IsSuccessStatusCode, "task GET " + path);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); return json.RootElement.Clone();
        }
        var deadline = DateTimeOffset.UtcNow.AddDays(7);
        using var anonymous = await http.GetAsync("/api/tasks");
        check(anonymous.StatusCode == HttpStatusCode.Unauthorized, "anonymous tasks rejected");
        await Login("task-mentor-a");
        var options = await Get("/api/tasks/interns");
        check(options.GetArrayLength()==1 && options[0].GetProperty("internId").GetInt32()==ids[0], "Mentor sees only currently assigned intern options");
        using var wrong = await http.PostAsJsonAsync("/api/tasks",new { internId=ids[1], content="Forbidden task", deadline });
        using var missing = await http.PostAsJsonAsync("/api/tasks",new { internId=int.MaxValue, content="Missing intern", deadline });
        check(wrong.StatusCode==HttpStatusCode.Forbidden && missing.StatusCode==HttpStatusCode.Forbidden, "foreign or nonexistent Intern cannot receive Mentor task");
        using var blank = await http.PostAsJsonAsync("/api/tasks",new { internId=ids[0], content="   ", deadline });
        using var expired = await http.PostAsJsonAsync("/api/tasks",new { internId=ids[0], content="Expired", deadline=DateTimeOffset.UtcNow.AddDays(-1) });
        using var longContent = await http.PostAsJsonAsync("/api/tasks",new { internId=ids[0], content=new string('a',2001), deadline });
        check(blank.StatusCode==HttpStatusCode.BadRequest && expired.StatusCode==HttpStatusCode.BadRequest && longContent.StatusCode==HttpStatusCode.BadRequest, "invalid content length and deadline rejected");
        using var created = await http.PostAsJsonAsync("/api/tasks",new { internId=ids[0], content="Hoàn thành báo cáo tiếng Việt", deadline, mentorUserId=99999, status="DONE" });
        check(created.StatusCode==HttpStatusCode.Created, "Mentor assigns task to own Intern");
        using var createJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync()); var taskId = createJson.RootElement.GetProperty("id").GetInt32();
        var own = await Get("/api/tasks");
        check(own.GetArrayLength()==1 && own[0].GetProperty("status").GetString()=="ASSIGNED" && own[0].GetProperty("mentorName").GetString()=="Mentor A"
            && own[0].GetProperty("content").GetString()=="Hoàn thành báo cáo tiếng Việt", "Mentor sees persisted task; server owns status and creator");
        command.CommandText="DELETE dbo.RolePermissions WHERE Role='MENTOR' AND Permission='TASK_CREATE'"; await command.ExecuteNonQueryAsync();
        using var forbidden = await http.PostAsJsonAsync("/api/tasks",new { internId=ids[0], content="Forbidden", deadline });
        check(forbidden.StatusCode==HttpStatusCode.Forbidden, "revoked create permission returns 403 immediately");
        await restart();
        await Login("task-mentor-a");
        var durable = await Get("/api/tasks");
        check(durable.GetArrayLength()==1 && durable[0].GetProperty("id").GetInt32()==taskId, "backend restart retains task in DB");
        using var stillForbidden = await http.PostAsJsonAsync("/api/tasks",new { internId=ids[0], content="Forbidden", deadline });
        check(stillForbidden.StatusCode==HttpStatusCode.Forbidden, "restart does not restore revoked task permission");
        command.CommandText="INSERT dbo.RolePermissions(Role,Permission) VALUES('MENTOR','TASK_CREATE')"; await command.ExecuteNonQueryAsync();
        await Login("task-mentor-b");
        check((await Get("/api/tasks")).GetArrayLength()==0, "other Mentor cannot list task");
        using var otherMentor = await http.GetAsync("/api/tasks/"+taskId);
        check(otherMentor.StatusCode==HttpStatusCode.NotFound, "other Mentor cannot read task by ID");
        await Login("task-intern-a");
        check((await Get("/api/tasks?internId="+ids[1])).GetArrayLength()==1, "Intern sees own task regardless of supplied owner query");
        await Get("/api/tasks/"+taskId);
        using var internCreate = await http.PostAsJsonAsync("/api/tasks",new { internId=ids[0], content="Forbidden", deadline });
        check(internCreate.StatusCode==HttpStatusCode.Forbidden, "Intern cannot create task");
        await Login("task-intern-b");
        check((await Get("/api/tasks?internId="+ids[0])).GetArrayLength()==0, "Intern cannot list another Intern's tasks");
        using var otherIntern = await http.GetAsync("/api/tasks/"+taskId);
        check(otherIntern.StatusCode==HttpStatusCode.NotFound, "Intern cannot read another Intern's task by ID");
        command.CommandText="DELETE dbo.RolePermissions WHERE Role='INTERN' AND Permission='OWN_TASK_READ'"; await command.ExecuteNonQueryAsync();
        using var noRead = await http.GetAsync("/api/tasks");
        check(noRead.StatusCode==HttpStatusCode.Forbidden, "Intern missing read permission receives 403");
        // Move the existing assignment; old tasks retain their creator, new tasks use current assignment.
        command.CommandText=$"UPDATE dbo.Interns SET MentorUserId=(SELECT Id FROM dbo.Users WHERE Username='task-mentor-b') WHERE Id={ids[0]}";
        await command.ExecuteNonQueryAsync();
        await Login("task-mentor-a");
        using var reassigned = await http.PostAsJsonAsync("/api/tasks",new { internId=ids[0], content="Former mentor", deadline });
        check(reassigned.StatusCode==HttpStatusCode.Forbidden, "former Mentor cannot assign after US30 reassignment");
        check((await Get("/api/tasks")).GetArrayLength()==1, "former Mentor retains own assigned task history");
        await Login("task-mentor-b");
        using var newMentor = await http.PostAsJsonAsync("/api/tasks",new { internId=ids[0], content="New Mentor task", deadline });
        check(newMentor.StatusCode==HttpStatusCode.Created, "new Mentor reuses current US30 relationship");
        await Login("task-hr");
        using var hrRead = await http.GetAsync("/api/tasks");
        check(hrRead.StatusCode==HttpStatusCode.Forbidden, "HR cannot bypass task permission scope");
        command.CommandText="SELECT COUNT(*) FROM dbo.InternTasks";
        check(Convert.ToInt32(await command.ExecuteScalarAsync())==2, "rejected requests create no task rows");
    }
}
