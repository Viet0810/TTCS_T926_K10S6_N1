using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;

internal static class LeaveRequestChecks
{
    public static async Task RunAsync(HttpClient http, string connectionString, Action<bool, string> check,
        Func<string, Task> login, Func<string, Task<JsonElement>> get)
    {
        const string path = "/api/interns/me/leave-requests";
        var payload = new { leaveType = "SICK_LEAVE", durationType = "FULL_DAY", startDate = "2026-10-20", endDate = "2026-10-20", reason = "Ownership regression leave request" };
        http.DefaultRequestHeaders.Authorization = null;
        using var anonymous = await http.GetAsync(path);
        check(anonymous.StatusCode == HttpStatusCode.Unauthorized, "US24 anonymous request rejected");
        await login("ownership-intern-a");
        using var created = await http.PostAsJsonAsync(path, payload);
        check(created.StatusCode == HttpStatusCode.Created, "US24 Intern creates own request with different UserId/InternId");
        using var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = body.RootElement.GetProperty("id").GetInt64();
        var own = await get(path);
        check(own.GetProperty("items").GetArrayLength() == 1 && own.GetProperty("items")[0].GetProperty("id").GetInt64() == id,
            "US24 Intern lists own persisted request");
        await login("ownership-intern-b");
        check((await get(path + "?userId=3&internId=1")).GetProperty("items").GetArrayLength() == 0, "US24 other Intern cannot list foreign requests through query parameters");
        using var foreignCancel = await http.PostAsync($"{path}/{id}/cancel", null);
        check(foreignCancel.StatusCode == HttpStatusCode.Forbidden, "US24 foreign cancellation returns 403");
        await using var sql = new SqlConnection(connectionString);
        await sql.OpenAsync();
        await using var command = sql.CreateCommand();
        command.CommandText = $"SELECT Status FROM dbo.LeaveRequests WHERE Id={id}";
        check((string?)await command.ExecuteScalarAsync() == "PENDING", "US24 foreign cancellation leaves database unchanged");
        await login("ownership-unmapped");
        using var noMappingList = await http.GetAsync(path);
        using var noMappingCreate = await http.PostAsJsonAsync(path, payload);
        using var noMappingCancel = await http.PostAsync($"{path}/{id}/cancel", null);
        check(noMappingList.StatusCode == HttpStatusCode.Forbidden && noMappingCreate.StatusCode == HttpStatusCode.Forbidden
            && noMappingCancel.StatusCode == HttpStatusCode.Forbidden, "US24 missing email mapping returns 403 despite colliding UserId/InternId");
        command.CommandText = "SELECT COUNT(*) FROM dbo.LeaveRequests WHERE UserId=(SELECT Id FROM dbo.Users WHERE Username='ownership-unmapped')";
        check(Convert.ToInt32(await command.ExecuteScalarAsync()) == 0, "US24 invalid mapping creates no leave rows");
        await login("ownership-intern-a");
        // Corrupted/legacy rows matching only one of the two owners must also be denied.
        command.CommandText = $"UPDATE dbo.LeaveRequests SET UserId=(SELECT Id FROM dbo.Users WHERE Username='ownership-intern-b') WHERE Id={id}";
        await command.ExecuteNonQueryAsync();
        check((await get(path)).GetProperty("items").GetArrayLength() == 0, "US24 list requires both UserId and mapped InternId");
        using var mixedOwner = await http.PostAsync($"{path}/{id}/cancel", null);
        check(mixedOwner.StatusCode == HttpStatusCode.Forbidden, "US24 matching InternId alone cannot authorize cancellation");
        await login("ownership-intern-b");
        using var mixedUser = await http.PostAsync($"{path}/{id}/cancel", null);
        check(mixedUser.StatusCode == HttpStatusCode.Forbidden, "US24 matching UserId alone cannot authorize cancellation");
        command.CommandText = $"UPDATE dbo.LeaveRequests SET UserId=(SELECT Id FROM dbo.Users WHERE Username='ownership-intern-a') WHERE Id={id}";
        await command.ExecuteNonQueryAsync();
        await login("ownership-intern-a");
        using var cancelled = await http.PostAsync($"{path}/{id}/cancel", null);
        check(cancelled.StatusCode == HttpStatusCode.OK, "US24 owner cancels own pending request");
        command.CommandText = $"SELECT Status FROM dbo.LeaveRequests WHERE Id={id}";
        check((string?)await command.ExecuteScalarAsync() == "CANCELLED", "US24 cancellation persisted in DB");
        using var missing = await http.PostAsync($"{path}/{long.MaxValue}/cancel", null);
        check(missing.StatusCode == HttpStatusCode.NotFound, "US24 nonexistent request remains 404");
        using var invalid = await http.PostAsJsonAsync(path, new { leaveType="INVALID", durationType="FULL_DAY", startDate="2026-10-20", reason="Invalid request" });
        check(invalid.StatusCode == HttpStatusCode.BadRequest, "US24 existing validation preserved");
        await login("ownership-hr");
        using var hrRead = await http.GetAsync(path);
        using var hrCreate = await http.PostAsJsonAsync(path, payload);
        using var hrCancel = await http.PostAsync($"{path}/{id}/cancel", null);
        check(hrRead.StatusCode == HttpStatusCode.Forbidden && hrCreate.StatusCode == HttpStatusCode.Forbidden
            && hrCancel.StatusCode == HttpStatusCode.Forbidden, "US24 non-Intern cannot use own-leave endpoints");

        var approvePayload = new { leaveType = "PERSONAL_LEAVE", durationType = "FULL_DAY", startDate = "2026-10-21", endDate = "2026-10-21", reason = "Mentor approval ownership check" };
        await login("ownership-intern-a");
        using var approveCreated = await http.PostAsJsonAsync(path, approvePayload);
        check(approveCreated.StatusCode == HttpStatusCode.Created, "Intern creates leave request for Mentor review");
        using var approveBody = JsonDocument.Parse(await approveCreated.Content.ReadAsStringAsync());
        var approveId = approveBody.RootElement.GetProperty("id").GetInt64();
        command.CommandText = "SELECT COUNT(*) FROM dbo.Notifications WHERE UserId=(SELECT Id FROM dbo.Users WHERE Username='ownership-mentor-b') AND Type='LEAVE_PENDING_REVIEW' AND Message LIKE '%21/10/2026%'";
        check(Convert.ToInt32(await command.ExecuteScalarAsync()) == 1, "Assigned Mentor receives pending leave notification");

        const string mentorPath = "/api/mentor/leave-requests";
        await login("ownership-mentor-a");
        using var wrongDetail = await http.GetAsync($"{mentorPath}/{approveId}");
        using var wrongApprove = await http.PostAsJsonAsync($"{mentorPath}/{approveId}/review", new { decision = "APPROVED", note = "wrong mentor" });
        check(wrongDetail.StatusCode == HttpStatusCode.Forbidden && wrongApprove.StatusCode == HttpStatusCode.Forbidden,
            "Wrong Mentor cannot view or review leave request");

        await login("ownership-mentor-b");
        var mentorList = await get(mentorPath + "?status=PENDING");
        check(mentorList.EnumerateArray().Any(x => x.GetProperty("id").GetInt64() == approveId), "Assigned Mentor sees pending leave request");
        using var approved = await http.PostAsJsonAsync($"{mentorPath}/{approveId}/review", new { decision = "APPROVED", note = "Approved by assigned Mentor" });
        check(approved.StatusCode == HttpStatusCode.OK, "Assigned Mentor approves leave request");
        using var secondApprove = await http.PostAsJsonAsync($"{mentorPath}/{approveId}/review", new { decision = "APPROVED", note = "duplicate" });
        check(secondApprove.StatusCode == HttpStatusCode.Conflict, "Processed leave request cannot be reviewed twice");

        await login("ownership-intern-a");
        var approvedOwn = await get(path + "?status=APPROVED");
        check(approvedOwn.GetProperty("items").EnumerateArray().Any(x => x.GetProperty("id").GetInt64() == approveId), "Intern sees approved result");
        command.CommandText = "SELECT COUNT(*) FROM dbo.Notifications WHERE UserId=(SELECT Id FROM dbo.Users WHERE Username='ownership-intern-a') AND Type='LEAVE_APPROVED'";
        check(Convert.ToInt32(await command.ExecuteScalarAsync()) == 1, "Intern receives approval notification");

        var rejectPayload = new { leaveType = "OTHER", durationType = "FULL_DAY", startDate = "2026-10-22", endDate = "2026-10-22", reason = "Mentor rejection ownership check" };
        using var rejectCreated = await http.PostAsJsonAsync(path, rejectPayload);
        using var rejectBody = JsonDocument.Parse(await rejectCreated.Content.ReadAsStringAsync());
        var rejectId = rejectBody.RootElement.GetProperty("id").GetInt64();
        await login("ownership-mentor-b");
        using var rejected = await http.PostAsJsonAsync($"{mentorPath}/{rejectId}/review", new { decision = "REJECTED", note = "Insufficient handover" });
        check(rejected.StatusCode == HttpStatusCode.OK, "Assigned Mentor rejects leave request");
        await login("ownership-intern-a");
        var rejectedOwn = await get(path + "?status=REJECTED");
        check(rejectedOwn.GetProperty("items").EnumerateArray().Any(x => x.GetProperty("id").GetInt64() == rejectId
            && x.GetProperty("reviewerNote").GetString() == "Insufficient handover"), "Intern sees rejected result and Mentor note");
        command.CommandText = "SELECT COUNT(*) FROM dbo.Notifications WHERE UserId=(SELECT Id FROM dbo.Users WHERE Username='ownership-intern-a') AND Type='LEAVE_REJECTED'";
        check(Convert.ToInt32(await command.ExecuteScalarAsync()) == 1, "Intern receives rejection notification");
    }
}
