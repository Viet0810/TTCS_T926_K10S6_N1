using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InternManagement.Services;
using Microsoft.Data.SqlClient;

internal static class ChangePasswordChecks
{
    public static async Task RunAsync(HttpClient http, string connectionString, Action<bool, string> check)
    {
        const string current = "Temporary-test-9", next = "Replacement-test-9";
        async Task<JsonElement> Login(string username, string password)
        {
            http.DefaultRequestHeaders.Authorization = null;
            using var response = await http.PostAsJsonAsync("/api/auth/login", new { username, password });
            check(response.StatusCode == HttpStatusCode.OK, "change password login " + username);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.RootElement.GetProperty("token").GetString());
            return json.RootElement.Clone();
        }
        http.DefaultRequestHeaders.Authorization = null;
        using var anonymous = await http.PutAsJsonAsync("/api/account/change-password", new { currentPassword = current, newPassword = next, confirmPassword = next });
        check(anonymous.StatusCode == HttpStatusCode.Unauthorized, "password change rejects anonymous");
        await using var sql = new SqlConnection(connectionString); await sql.OpenAsync();
        foreach (var role in new[] { "ADMIN", "HR", "MENTOR", "INTERN" })
        {
            var email = "change-" + role.ToLowerInvariant() + "@gmail.com";
            await Login("permission-admin", "Permission-test-9");
            if (role == "ADMIN")
            {
                await using var insert = sql.CreateCommand();
                insert.CommandText = "INSERT INTO dbo.Users(Username,FullName,Email,PasswordHash,Role) VALUES(@email,'Password test',@email,@hash,'ADMIN')";
                insert.Parameters.AddWithValue("@email",email); insert.Parameters.AddWithValue("@hash",new PasswordHasher().Hash(current)); await insert.ExecuteNonQueryAsync();
            }
            else
            {
                using var create = role is "HR" or "MENTOR"
                    ? await http.PostAsJsonAsync("/api/users", new { fullName="Password test", email, role })
                    : await http.PostAsJsonAsync("/api/users", new { fullName="Password test", email, password=current, role });
                check(create.StatusCode == HttpStatusCode.Created, role + " account created via admin API");
                // Mail content is covered by SMTP capture tests; use a known password for HTTP-only fixtures.
                if (role is "HR" or "MENTOR")
                {
                    await using var fixture = sql.CreateCommand();
                    fixture.CommandText = "UPDATE dbo.Users SET PasswordHash=@hash WHERE Email=@email";
                    fixture.Parameters.AddWithValue("@hash",new PasswordHasher().Hash(current)); fixture.Parameters.AddWithValue("@email",email);
                    await fixture.ExecuteNonQueryAsync();
                }
            }
            var session = await Login(email, current);
            check(session.GetProperty("user").GetProperty("mustChangePassword").GetBoolean() == (role is "HR" or "MENTOR"), role + " first-login flag correct");
            var oldToken = http.DefaultRequestHeaders.Authorization;
            if (role is "HR" or "MENTOR")
            {
                using var blocked = await http.GetAsync("/api/interns/me");
                using var blockedJson = JsonDocument.Parse(await blocked.Content.ReadAsStringAsync());
                check(blocked.StatusCode == HttpStatusCode.Forbidden && blockedJson.RootElement.GetProperty("code").GetString() == "PASSWORD_CHANGE_REQUIRED", "temporary session cannot bypass business API");
            }
            foreach (var invalid in new[] {
                new { currentPassword="Wrong-test-9", newPassword=next, confirmPassword=next },
                new { currentPassword=current, newPassword="weak", confirmPassword="weak" },
                new { currentPassword=current, newPassword=next, confirmPassword="Mismatch-test-9" },
                new { currentPassword=current, newPassword=current, confirmPassword=current } })
            {
                using var denied = await http.PutAsJsonAsync("/api/account/change-password", invalid);
                check(denied.StatusCode == HttpStatusCode.BadRequest, role + " invalid current/weak/mismatch/reused password rejected");
            }
            using var change = await http.PutAsJsonAsync("/api/account/change-password", new { currentPassword=current, newPassword=next, confirmPassword=next, userId=1, role="ADMIN" });
            check(change.StatusCode == HttpStatusCode.OK, role + " changes own password");
            using var result = JsonDocument.Parse(await change.Content.ReadAsStringAsync());
            check(!result.RootElement.GetProperty("user").GetProperty("mustChangePassword").GetBoolean(), "successful change clears required flag");
            http.DefaultRequestHeaders.Authorization = oldToken;
            using var revoked = await http.GetAsync("/api/auth/me");
            check(revoked.StatusCode == HttpStatusCode.Unauthorized, "old session invalidated after change");
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",result.RootElement.GetProperty("token").GetString());
            using var me = await http.GetAsync("/api/auth/me");
            check(me.StatusCode == HttpStatusCode.OK, "replacement session remains usable");
            http.DefaultRequestHeaders.Authorization = null;
            using var oldLogin = await http.PostAsJsonAsync("/api/auth/login",new { username=email,password=current });
            check(oldLogin.StatusCode == HttpStatusCode.Unauthorized,"old password no longer authenticates");
            await Login(email,next);
            using var resend = await http.PostAsync("/api/users/1/resend-login-email",null);
            if(role!="ADMIN")check(resend.StatusCode==HttpStatusCode.Forbidden,"non-admin cannot resend credentials");
        }
        await Login("permission-hr","Permission-test-9");
    }
}
