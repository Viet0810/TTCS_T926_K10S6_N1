using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;

internal static class InternAssignmentChecks
{
    public static async Task RunAsync(HttpClient http, string connectionString, Action<bool, string> check)
    {
        async Task Login(string username, string password = "Permission-test-9")
        {
            http.DefaultRequestHeaders.Authorization = null;
            using var response = await http.PostAsJsonAsync("/api/auth/login", new { username, password });
            check(response.StatusCode == HttpStatusCode.OK, "assignment test login " + username);
            using var data = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", data.RootElement.GetProperty("token").GetString());
        }
        async Task<JsonElement> Get(string path)
        {
            using var response = await http.GetAsync(path);
            check(response.StatusCode == HttpStatusCode.OK, "assignment GET " + path);
            using var data = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return data.RootElement.Clone();
        }
        http.DefaultRequestHeaders.Authorization = null;
        foreach (var path in new[] { "/api/intern-assignments", "/api/intern-assignments/interns", "/api/intern-assignments/mentors", "/api/intern-assignments/programs", "/api/interns/me/schedule" })
        {
            using var response = await http.GetAsync(path);
            check(response.StatusCode == HttpStatusCode.Unauthorized, "anonymous rejected " + path);
        }
        var internIds = new List<int>();
        foreach (var suffix in new[] { "a", "b" })
        {
            using var response = await http.PostAsJsonAsync("/api/auth/register", new
            {
                fullName = "Schedule Intern " + suffix, email = "schedule-" + suffix + "@ictu.edu.vn",
                password = "Schedule-test-9", phone = "0912345678", school = "ICTU", major = "IT"
            });
            check(response.StatusCode == HttpStatusCode.Created, "register schedule intern " + suffix);
        }
        await using var sql = new SqlConnection(connectionString);
        await sql.OpenAsync();
        await using var lookup = sql.CreateCommand();
        lookup.CommandText = "SELECT Id FROM dbo.Interns WHERE Email IN ('schedule-a@ictu.edu.vn', 'schedule-b@ictu.edu.vn') ORDER BY Email";
        await using (var reader = await lookup.ExecuteReaderAsync())
            while (await reader.ReadAsync()) internIds.Add(reader.GetInt32(0));
        lookup.CommandText = "SELECT Id FROM dbo.Users WHERE Username = 'permission-mentor'";
        var mentorId = Convert.ToInt32(await lookup.ExecuteScalarAsync());
        lookup.CommandText = """
            INSERT INTO dbo.Users(Username, FullName, Email, PasswordHash, Role)
            OUTPUT INSERTED.Id
            SELECT 'assignment-mentor', 'Replacement Mentor', 'assignment-mentor@gmail.com', PasswordHash, 'MENTOR'
            FROM dbo.Users WHERE Id = @MentorId
            """;
        lookup.Parameters.AddWithValue("@MentorId", mentorId);
        var replacementMentorId = Convert.ToInt32(await lookup.ExecuteScalarAsync());
        lookup.Parameters.Clear();
        lookup.CommandText = "SELECT Id FROM dbo.Users WHERE Username = 'permission-hr'";
        var hrId = Convert.ToInt32(await lookup.ExecuteScalarAsync());
        lookup.CommandText = "UPDATE dbo.Interns SET Department = 'IT', StartDate = '2020-01-01', EndDate = '2020-01-02' WHERE Email = 'schedule-a@ictu.edu.vn'";
        await lookup.ExecuteNonQueryAsync();

        await Login("permission-hr");
        var session = await Get("/api/auth/me");
        check(session.GetProperty("permissions").EnumerateArray().Any(p => p.GetString() == "ASSIGN_MENTOR"), "HR has real ASSIGN_MENTOR permission");
        var interns = await Get("/api/intern-assignments/interns");
        check(interns.EnumerateArray().Any(i => i.GetProperty("internId").GetInt32() == internIds[0]), "HR can select registered intern");
        var mentors = await Get("/api/intern-assignments/mentors");
        check(mentors.EnumerateArray().Any(m => m.GetProperty("id").GetInt32() == mentorId)
            && !mentors.EnumerateArray().Any(m => m.GetProperty("id").GetInt32() == hrId), "mentor options exclude non-MENTOR users");
        using var programCreate = await http.PostAsJsonAsync("/api/program-schedule", new { startDate = "2026-10-01", endDate = "2026-12-31" });
        using var program = JsonDocument.Parse(await programCreate.Content.ReadAsStringAsync());
        var programId = program.RootElement.GetProperty("data").GetProperty("id").GetInt32();
        var programs = await Get("/api/intern-assignments/programs");
        check(programs.EnumerateArray().Any(p => p.GetProperty("id").GetInt32() == programId), "HR selects US13 program");
        var assignment = new { mentorUserId = mentorId, internshipProgramId = programId, role = "ADMIN" };
        foreach (var blockedStatus in new[] { "Đã hoàn thành", "Đã dừng", "Rejected", "Pending" })
        {
            lookup.CommandText = "UPDATE dbo.Interns SET Status = @Status WHERE Id = @Id";
            lookup.Parameters.AddWithValue("@Status", blockedStatus);
            lookup.Parameters.AddWithValue("@Id", internIds[0]);
            await lookup.ExecuteNonQueryAsync();
            using var blocked = await http.PutAsJsonAsync($"/api/intern-assignments/{internIds[0]}", assignment);
            check(blocked.StatusCode == HttpStatusCode.BadRequest, "blocked assignment status " + blockedStatus);
            lookup.CommandText = "SELECT COUNT(*) FROM dbo.Interns WHERE Id = @Id AND Status = @Status AND MentorUserId IS NULL AND InternshipProgramId IS NULL";
            check(Convert.ToInt32(await lookup.ExecuteScalarAsync()) == 1, "failed assignment leaves status and links unchanged");
            lookup.Parameters.Clear();
        }
        foreach (var eligibleStatus in new string?[] { null, "Chờ tiếp nhận", "Đang thực tập" })
        {
            lookup.CommandText = "UPDATE dbo.Interns SET Status = @Status WHERE Id = @Id";
            lookup.Parameters.AddWithValue("@Status", (object?)eligibleStatus ?? DBNull.Value);
            lookup.Parameters.AddWithValue("@Id", internIds[0]);
            await lookup.ExecuteNonQueryAsync();
            using var assigned = await http.PutAsJsonAsync($"/api/intern-assignments/{internIds[0]}", assignment);
            check(assigned.StatusCode == HttpStatusCode.OK, "eligible assignment status " + (eligibleStatus ?? "unset"));
            lookup.CommandText = "SELECT COUNT(*) FROM dbo.Interns WHERE Id = @Id AND Status = N'Đang thực tập' AND MentorUserId = @MentorId AND InternshipProgramId = @ProgramId";
            lookup.Parameters.AddWithValue("@MentorId", mentorId);
            lookup.Parameters.AddWithValue("@ProgramId", programId);
            check(Convert.ToInt32(await lookup.ExecuteScalarAsync()) == 1, "assignment and status persist together");
            lookup.Parameters.Clear();
        }
        using var save = await http.PutAsJsonAsync($"/api/intern-assignments/{internIds[0]}", assignment);
        check(save.StatusCode == HttpStatusCode.OK, "HR saves intern-mentor-program assignment: " + (int)save.StatusCode + " " + await save.Content.ReadAsStringAsync());
        foreach (var response in await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => http.PutAsJsonAsync($"/api/intern-assignments/{internIds[0]}", assignment))))
        {
            using (response) check(response.StatusCode == HttpStatusCode.OK, "repeated/concurrent assignment is idempotent");
        }
        var assignments = await Get("/api/intern-assignments");
        var current = assignments.EnumerateArray().Where(a => a.GetProperty("internId").GetInt32() == internIds[0]).ToArray();
        check(current.Length == 1 && current[0].GetProperty("mentorUserId").GetInt32() == mentorId
            && current[0].GetProperty("status").GetString() == "Đang thực tập", "refresh returns one durable assignment and updated status");
        using var invalidIntern = await http.PutAsJsonAsync("/api/intern-assignments/2147483647", assignment);
        using var invalidMentor = await http.PutAsJsonAsync($"/api/intern-assignments/{internIds[0]}", new { mentorUserId = int.MaxValue, internshipProgramId = programId });
        using var wrongRole = await http.PutAsJsonAsync($"/api/intern-assignments/{internIds[0]}", new { mentorUserId = hrId, internshipProgramId = programId });
        using var invalidProgram = await http.PutAsJsonAsync($"/api/intern-assignments/{internIds[0]}", new { mentorUserId = mentorId, internshipProgramId = int.MaxValue });
        using var missingMentor = await http.PutAsJsonAsync($"/api/intern-assignments/{internIds[0]}", new { internshipProgramId = programId });
        check(invalidIntern.StatusCode == HttpStatusCode.NotFound && invalidMentor.StatusCode == HttpStatusCode.BadRequest
            && wrongRole.StatusCode == HttpStatusCode.BadRequest && invalidProgram.StatusCode == HttpStatusCode.BadRequest
            && missingMentor.StatusCode == HttpStatusCode.BadRequest, "invalid intern/mentor/role/program rejected without mutation");
        var unchanged = await Get("/api/intern-assignments");
        check(unchanged.EnumerateArray().Single(a => a.GetProperty("internId").GetInt32() == internIds[0]).GetProperty("mentorUserId").GetInt32() == mentorId
            && unchanged.EnumerateArray().Single(a => a.GetProperty("internId").GetInt32() == internIds[0]).GetProperty("status").GetString() == "Đang thực tập",
            "invalid saves preserve existing assignment");
        using var reassign = await http.PutAsJsonAsync($"/api/intern-assignments/{internIds[0]}", new { mentorUserId = replacementMentorId, internshipProgramId = programId });
        check(reassign.StatusCode == HttpStatusCode.OK, "HR reassigns to another real mentor");
        var refreshed = await Get("/api/intern-assignments");
        var replaced = refreshed.EnumerateArray().Where(a => a.GetProperty("internId").GetInt32() == internIds[0]).ToArray();
        check(replaced.Length == 1 && replaced[0].GetProperty("mentorUserId").GetInt32() == replacementMentorId
            && replaced[0].GetProperty("status").GetString() == "Đang thực tập", "reassignment preserves status without duplicate");

        await Login("schedule-a@ictu.edu.vn", "Schedule-test-9");
        var schedule = await Get("/api/interns/me/schedule");
        check(schedule.GetProperty("internId").GetInt32() == internIds[0]
            && schedule.GetProperty("startDate").GetString() == "2026-10-01"
            && schedule.GetProperty("endDate").GetString() == "2026-12-31", "own dates come from US13 program, not legacy profile dates");
        check(schedule.GetProperty("mentorName").GetString() == "Replacement Mentor"
            && schedule.GetProperty("departmentName").GetString() == "IT", "own schedule shows real assigned mentor and department");
        var query = await Get($"/api/interns/me/schedule?internId={internIds[1]}");
        check(query.GetProperty("internId").GetInt32() == internIds[0], "client cannot override schedule owner");
        using var otherSchedule = await http.GetAsync($"/api/interns/{internIds[1]}/schedule");
        using var otherProfile = await http.GetAsync($"/api/interns/{internIds[1]}");
        check(otherSchedule.StatusCode == HttpStatusCode.NotFound && otherProfile.StatusCode == HttpStatusCode.Forbidden, "intern cannot access another intern schedule/profile");
        using var selfAssign = await http.PutAsJsonAsync($"/api/intern-assignments/{internIds[0]}", assignment);
        check(selfAssign.StatusCode == HttpStatusCode.Forbidden, "INTERN cannot self-assign");
        await Login("schedule-b@ictu.edu.vn", "Schedule-test-9");
        var empty = await Get("/api/interns/me/schedule");
        check(empty.GetProperty("startDate").ValueKind == JsonValueKind.Null && empty.GetProperty("endDate").ValueKind == JsonValueKind.Null, "unassigned intern has empty schedule");
        foreach (var role in new[] { "admin", "mentor", "hr" })
        {
            await Login("permission-" + role);
            using var forbiddenSchedule = await http.GetAsync("/api/interns/me/schedule");
            check(forbiddenSchedule.StatusCode == HttpStatusCode.Forbidden, role + " cannot use own intern schedule API");
            if (role != "hr")
            {
                using var forbiddenAssignment = await http.PutAsJsonAsync($"/api/intern-assignments/{internIds[0]}", assignment);
                using var forbiddenList = await http.GetAsync("/api/intern-assignments/mentors");
                check(forbiddenAssignment.StatusCode == HttpStatusCode.Forbidden && forbiddenList.StatusCode == HttpStatusCode.Forbidden, role + " cannot assign or list assignment options");
            }
        }
        // Removing a US13 program keeps assignment and profile intact and clears its schedule link.
        using var deleteProgram = await http.DeleteAsync($"/api/program-schedule/{programId}");
        check(deleteProgram.StatusCode == HttpStatusCode.OK, "US13 deletion remains compatible with linked interns");
        await Login("schedule-a@ictu.edu.vn", "Schedule-test-9");
        var afterDelete = await Get("/api/interns/me/schedule");
        check(afterDelete.GetProperty("startDate").ValueKind == JsonValueKind.Null
            && afterDelete.GetProperty("mentorName").ValueKind == JsonValueKind.String, "deleted program clears dates without losing mentor assignment");
        await Login("permission-admin");
        using var deleteMentor = await http.DeleteAsync($"/api/users/{replacementMentorId}");
        check(deleteMentor.StatusCode == HttpStatusCode.NoContent, "existing account deletion clears mentor foreign key safely");
        await Login("schedule-a@ictu.edu.vn", "Schedule-test-9");
        var afterMentorDelete = await Get("/api/interns/me/schedule");
        check(afterMentorDelete.GetProperty("mentorName").ValueKind == JsonValueKind.Null, "deleted mentor is not displayed as a current assignment");
        await Login("permission-hr");
    }
}
