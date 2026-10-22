using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;

internal static class AccountChecks
{
    public static async Task RunAsync(HttpClient http, string connectionString, Action<bool, string> check)
    {
        var passwords = new InternManagement.Services.PasswordHasher();
        check(!passwords.Verify("irrelevant", "PBKDF2-SHA256$0$AA==$AA==")
            && !passwords.Verify("irrelevant", "PBKDF2-SHA256$100000$AA==$")
            && !passwords.Verify("irrelevant", "invalid"), "malformed stored password hashes fail closed without an exception");
        using var login = await http.PostAsJsonAsync("/api/auth/login", new { username = "permission-admin", password = "Permission-test-9" });
        using var session = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var user = session.RootElement.GetProperty("user");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.RootElement.GetProperty("token").GetString());

        var payload = new { fullName = "Account service test", email = "account-service@example.invalid", password = "Account-test-9", role = "intern" };
        using var created = await http.PostAsJsonAsync("/api/users", payload);
        using var createdBody = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var createdUser = createdBody.RootElement.GetProperty("data");
        var id = createdUser.GetProperty("id").GetInt32();
        check(created.StatusCode == HttpStatusCode.Created && createdBody.RootElement.GetProperty("success").GetBoolean()
            && createdUser.GetProperty("role").GetString() == "INTERN", "account creation keeps response fields and normalized role");
        check(!createdUser.TryGetProperty("password", out _) && !createdUser.TryGetProperty("passwordHash", out _), "account response contains no credentials");
        using var fetched = JsonDocument.Parse(await http.GetStringAsync($"/api/users/{id}"));
        check(fetched.RootElement.GetProperty("email").GetString() == payload.email, "created account can be fetched using its Location route");
        check((await http.PostAsJsonAsync("/api/users", payload)).StatusCode == HttpStatusCode.Conflict, "duplicate account returns 409");
        check((await http.DeleteAsync($"/api/users/{user.GetProperty("id").GetInt32()}")).StatusCode == HttpStatusCode.BadRequest, "current account cannot delete itself");
        using var deleted = await http.DeleteAsync($"/api/users/{id}");
        check(deleted.StatusCode == HttpStatusCode.NoContent && (await deleted.Content.ReadAsStringAsync()).Length == 0, "account deletion preserves HTTP 204 contract");
        check((await http.GetAsync($"/api/users/{id}")).StatusCode == HttpStatusCode.NotFound, "deleted account returns 404");

        await using var sql = new SqlConnection(connectionString);
        await sql.OpenAsync();
        await using (var trigger = sql.CreateCommand())
        {
            trigger.CommandText = "CREATE TRIGGER dbo.AccountTestFailure ON dbo.Users AFTER INSERT AS THROW 51000, 'Private account failure', 1;";
            await trigger.ExecuteNonQueryAsync();
        }
        try
        {
            http.DefaultRequestHeaders.Add("Origin", "http://localhost:5500");
            using var failure = await http.PostAsJsonAsync("/api/users", payload);
            var body = await failure.Content.ReadAsStringAsync();
            using var error = JsonDocument.Parse(body);
            check(failure.StatusCode == HttpStatusCode.InternalServerError && !error.RootElement.GetProperty("success").GetBoolean()
                && error.RootElement.TryGetProperty("message", out _) && error.RootElement.TryGetProperty("data", out _)
                && !body.Contains("Private account") && !body.Contains("SqlException"), "unhandled account failure returns existing safe error shape");
            check(failure.Headers.TryGetValues("X-Request-ID", out var traces) && traces.Single().Length > 0, "server error includes a correlation header");
            check(failure.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins) && origins.Single() == "*",
                "server failure retains CORS headers for frontend feedback");
        }
        finally
        {
            http.DefaultRequestHeaders.Remove("Origin");
            await using var restore = sql.CreateCommand();
            restore.CommandText = "DROP TRIGGER dbo.AccountTestFailure";
            await restore.ExecuteNonQueryAsync();
        }
        http.DefaultRequestHeaders.Authorization = null;
    }
}
