using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InternManagement.Services;
using Microsoft.Data.SqlClient;

internal static class RolePermissionChecks
{
    public static async Task RunAsync(HttpClient http, string connectionString, Action<bool,string> check)
    {
        await using var sql = new SqlConnection(connectionString);
        await sql.OpenAsync();
        await using var select = sql.CreateCommand();
        select.CommandText = "SELECT TOP(1) Id, FullName, Email, Phone, School, Major FROM dbo.Interns ORDER BY Id";
        int id;
        string email,phone,school,major;
        await using (var reader = await select.ExecuteReaderAsync())
        {
            if(!await reader.ReadAsync()) throw new Exception("Missing isolated intern fixture.");
            id=reader.GetInt32(0);email=reader.GetString(2);phone=reader.GetString(3);school=reader.GetString(4);major=reader.GetString(5);
        }
        var payload = new {fullName="Edited through HR API",email,phone,school,major};
        using var noSearchSession = await http.GetAsync("/api/interns/search");
        check(noSearchSession.StatusCode==HttpStatusCode.Unauthorized,"search API rejects anonymous requests");
        using var noEditSession = await http.PutAsJsonAsync($"/api/interns/{id}",payload);
        check(noEditSession.StatusCode==HttpStatusCode.Unauthorized,"edit API rejects anonymous requests");

        foreach(var role in new[]{"ADMIN","HR","MENTOR","INTERN"})
        {
            var username="permission-"+role.ToLowerInvariant();
            await using var insert=sql.CreateCommand();
            insert.CommandText="INSERT INTO dbo.Users(Username,FullName,Email,PasswordHash,Role) VALUES(@username,@name,@email,@hash,@role)";
            insert.Parameters.AddWithValue("@username",username);
            insert.Parameters.AddWithValue("@name","Permission integration test");
            insert.Parameters.AddWithValue("@email",username+"@example.invalid");
            insert.Parameters.AddWithValue("@hash",new PasswordHasher().Hash("Permission-test-9"));
            insert.Parameters.AddWithValue("@role",role);
            await insert.ExecuteNonQueryAsync();
            using var login=await http.PostAsJsonAsync("/api/auth/login",new{username,password="Permission-test-9"});
            check(login.StatusCode==HttpStatusCode.OK,$"{role} can authenticate using SQL credentials");
            using var loginData=JsonDocument.Parse(await login.Content.ReadAsStringAsync());
            http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",loginData.RootElement.GetProperty("token").GetString());
            using var session=await http.GetAsync("/api/auth/me");
            check(session.StatusCode==HttpStatusCode.OK,$"{role} session is accepted by backend");
            using var sessionData=JsonDocument.Parse(await session.Content.ReadAsStringAsync());
            var permissions=sessionData.RootElement.GetProperty("permissions").EnumerateArray().Select(item=>item.GetString()).ToArray();
            check(permissions.Contains(PermissionNames.ManagePrograms)==(role=="HR")
                && permissions.Contains(PermissionNames.ViewAttendanceReport)==(role=="HR")
                && permissions.Contains(PermissionNames.ApproveDocuments)==(role=="HR")
                && permissions.Contains(PermissionNames.ViewDocuments)==(role=="INTERN"),
                $"{role} Sprint 2 permissions match implemented role scopes");
            using var programs = await http.GetAsync("/api/program-schedule");
            using var attendance = await http.GetAsync("/api/attendance/report");
            using var reviews = await http.GetAsync("/api/document-reviews");
            check(programs.StatusCode==(role=="HR"?HttpStatusCode.OK:HttpStatusCode.Forbidden)
                && attendance.StatusCode==(role=="HR"?HttpStatusCode.OK:HttpStatusCode.Forbidden)
                && reviews.StatusCode==(role=="HR"?HttpStatusCode.OK:HttpStatusCode.Forbidden),
                $"{role} cannot bypass HR-only Sprint 2 APIs");
            using var documents = await http.GetAsync("/api/interns/me/documents");
            check(role=="INTERN" ? documents.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound
                : documents.StatusCode==HttpStatusCode.Forbidden, $"{role} own-document API authorization is enforced");
            var searchAllowed=role is "ADMIN" or "HR" or "MENTOR";
            var editAllowed=role is "ADMIN" or "HR";
            check(permissions.Contains(PermissionNames.SearchInterns)==searchAllowed
                && permissions.Contains(PermissionNames.EditInterns)==editAllowed,$"{role} receives matching frontend search/edit permissions");
            var query=$"/api/interns/search?search={Uri.EscapeDataString(email)}&school={Uri.EscapeDataString(school)}&major={Uri.EscapeDataString(major)}";
            using var search=await http.GetAsync(query);
            check(search.StatusCode==(searchAllowed?HttpStatusCode.OK:HttpStatusCode.Forbidden),$"{role} search access is enforced on API");
            if(searchAllowed)
            {
                using var results=JsonDocument.Parse(await search.Content.ReadAsStringAsync());
                check(results.RootElement.GetArrayLength()==1 && results.RootElement[0].GetProperty("id").GetInt32()==id
                    && results.RootElement[0].TryGetProperty("fullName",out _),$"{role} search returns matching SQL data in frontend format");
            }
            using var update=await http.PutAsJsonAsync($"/api/interns/{id}",payload);
            check(update.StatusCode==(editAllowed?HttpStatusCode.OK:HttpStatusCode.Forbidden),$"{role} edit access is enforced on API");
            if(editAllowed)
            {
                using var updated=JsonDocument.Parse(await update.Content.ReadAsStringAsync());
                check(updated.RootElement.GetProperty("fullName").GetString()==payload.fullName,$"{role} edit response matches the frontend field names");
            }
            if(role=="HR")
            {
                using var users=await http.GetAsync("/api/users");
                using var roles=await http.GetAsync("/api/auth/roles/permissions");
                check(users.StatusCode==HttpStatusCode.Forbidden&&roles.StatusCode==HttpStatusCode.Forbidden,"HR does not receive account administration privileges");
                using var missing=await http.PutAsJsonAsync("/api/interns/2147483647",payload);
                check(missing.StatusCode==HttpStatusCode.NotFound,"editing a missing profile returns HTTP 404");
                using var invalid=await http.PutAsJsonAsync($"/api/interns/{id}",new{fullName="",email="bad",phone="1",school="",major=""});
                check(invalid.StatusCode==HttpStatusCode.BadRequest,"backend validates edited profile fields");
                using var tooLong=await http.GetAsync("/api/interns/search?school="+new string('x',201));
                check(tooLong.StatusCode==HttpStatusCode.BadRequest,"search filters honor database field lengths");
            }
        }
        http.DefaultRequestHeaders.Authorization=null;
        await using var verify=sql.CreateCommand();
        verify.CommandText="SELECT FullName FROM dbo.Interns WHERE Id=@id";
        verify.Parameters.AddWithValue("@id",id);
        check((string?)await verify.ExecuteScalarAsync()==payload.fullName,"authorized API edits are persisted in SQL Server");
    }
}
