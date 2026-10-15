using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InternManagement.Services;
using Microsoft.Data.SqlClient;

internal static class MentorChecks
{
    public static async Task RunAsync(HttpClient http, string connectionString, Action<bool, string> check)
    {
        var originalAuthorization = http.DefaultRequestHeaders.Authorization;
        var payload = new { fullName = "API Mentor test", email = "api-mentor@example.invalid" };

        try
        {
            http.DefaultRequestHeaders.Authorization = null;
            using (var anonymous = await http.PostAsJsonAsync("/api/mentors", payload))
                check(anonymous.StatusCode == HttpStatusCode.Unauthorized, "mentor creation rejects anonymous requests");

            foreach (var role in new[] { "ADMIN", "MENTOR", "INTERN" })
            {
                using var login = await http.PostAsJsonAsync("/api/auth/login", new
                {
                    username = $"permission-{role.ToLowerInvariant()}",
                    password = "Permission-test-9"
                });
                using var loginBody = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                    "Bearer",
                    loginBody.RootElement.GetProperty("token").GetString());

                using var denied = await http.PostAsJsonAsync("/api/mentors", payload);
                check(denied.StatusCode == HttpStatusCode.Forbidden, $"{role} cannot create a Mentor through the HR endpoint");
            }

            using (var login = await http.PostAsJsonAsync("/api/auth/login", new
                   {
                       username = "permission-hr",
                       password = "Permission-test-9"
                   }))
            using (var loginBody = JsonDocument.Parse(await login.Content.ReadAsStringAsync()))
            {
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                    "Bearer",
                    loginBody.RootElement.GetProperty("token").GetString());
            }

            using (var missingName = await http.PostAsJsonAsync(
                       "/api/mentors",
                       new { email = "missing-name@example.invalid" }))
                check(missingName.StatusCode == HttpStatusCode.BadRequest, "mentor creation requires a full name");

            using (var invalidEmail = await http.PostAsJsonAsync(
                       "/api/mentors",
                       new { fullName = "Invalid email", email = "not-an-email" }))
                check(invalidEmail.StatusCode == HttpStatusCode.BadRequest, "mentor creation validates email format");

            using var created = await http.PostAsJsonAsync("/api/mentors", new
            {
                fullName = "  API Mentor test  ",
                email = " api-mentor@example.invalid ",
                role = "ADMIN",
                password = "Client-password-9"
            });
            var createdText = await created.Content.ReadAsStringAsync();
            using var createdBody = JsonDocument.Parse(createdText);
            var createdData = createdBody.RootElement.GetProperty("data");
            var userId = createdData.GetProperty("id").GetInt32();
            check(created.StatusCode == HttpStatusCode.Created
                && createdBody.RootElement.GetProperty("accountCreated").GetBoolean()
                && createdData.GetProperty("role").GetString() == "MENTOR",
                "HR creates a Mentor account with the server-assigned role");
            check(created.Headers.Location?.ToString().EndsWith(
                    $"/api/users/{userId}",
                    StringComparison.Ordinal) == true,
                "mentor creation returns the new account location");
            check(createdData.GetProperty("fullName").GetString() == "API Mentor test"
                && createdData.GetProperty("email").GetString() == "api-mentor@example.invalid"
                && !createdText.Contains("Client-password-9", StringComparison.Ordinal)
                && !createdText.Contains("passwordHash", StringComparison.OrdinalIgnoreCase)
                && !createdData.TryGetProperty("password", out _),
                "mentor request is trimmed and response exposes no credentials");
            check(!createdBody.RootElement.GetProperty("emailSent").GetBoolean(),
                "mentor creation reports failed email delivery without undoing account creation");

            await using (var sql = new SqlConnection(connectionString))
            {
                await sql.OpenAsync();
                await using var verify = sql.CreateCommand();
                verify.CommandText = """
                    SELECT Role, MustChangePassword, PasswordHash
                    FROM dbo.Users
                    WHERE Id = @id;
                    """;
                verify.Parameters.AddWithValue("@id", userId);
                await using var reader = await verify.ExecuteReaderAsync();
                await reader.ReadAsync();
                var role = reader.GetString(0);
                var mustChangePassword = reader.GetBoolean(1);
                var passwordHash = reader.GetString(2);
                check(role == "MENTOR" && mustChangePassword
                    && passwordHash.StartsWith("PBKDF2-SHA256$", StringComparison.Ordinal)
                    && !new PasswordHasher().Verify("Client-password-9", passwordHash),
                    "Mentor account is stored with its required role and temporary-password state");
            }

            using (var duplicate = await http.PostAsJsonAsync("/api/mentors", payload))
                check(duplicate.StatusCode == HttpStatusCode.Conflict, "duplicate Mentor email returns HTTP 409");

            await using (var sql = new SqlConnection(connectionString))
            {
                await sql.OpenAsync();
                await using var cleanup = sql.CreateCommand();
                cleanup.CommandText = "DELETE FROM dbo.Users WHERE Id = @id";
                cleanup.Parameters.AddWithValue("@id", userId);
                await cleanup.ExecuteNonQueryAsync();
            }
        }
        finally
        {
            http.DefaultRequestHeaders.Authorization = originalAuthorization;
        }
    }

}
