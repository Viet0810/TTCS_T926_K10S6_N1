using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InternManagement.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

internal static class Sprint1ProfileChecks
{
    public static async Task RunAsync(HttpClient http, string connectionString, Action<bool,string> check)
    {
        var previous = http.DefaultRequestHeaders.Authorization;
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        { ["ConnectionStrings:InternManagement"] = connectionString }).Build();
        var permissions = new RolePermissionService(config);
        await using var sql = new SqlConnection(connectionString); await sql.OpenAsync();
        var hash = new PasswordHasher().Hash("Ownership-test-9");
        foreach(var email in new[]{"sprint1-owner@example.invalid","sprint1-other@example.invalid"})
        {
            await using var insert = sql.CreateCommand();
            insert.CommandText="INSERT dbo.Users(Username,Email,FullName,PasswordHash,Role) VALUES(@email,@email,N'Sprint 1 owner',@hash,'INTERN')";
            insert.Parameters.AddWithValue("@email",email); insert.Parameters.AddWithValue("@hash",hash); await insert.ExecuteNonQueryAsync();
        }
        async Task<string> Login(string username,string password)
        {
            using var response = await http.PostAsJsonAsync("/api/auth/login", new {username,password});
            using var data = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return data.RootElement.GetProperty("token").GetString()!;
        }
        var hr = await Login("permission-hr","Permission-test-9");
        http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",hr);
        try
        {
            using var invalid = await http.PostAsJsonAsync("/api/interns", new {fullName=(string?)null,email="bad",phone="1",school=" ",major=" "});
            check(invalid.StatusCode==HttpStatusCode.BadRequest,"profile null and blank required fields return validation errors");
            using var created = await http.PostAsJsonAsync("/api/interns",new {fullName="  Sprint 1 owner  ",email="  sprint1-owner@example.invalid  ",phone=" 0912345678 ",school="  Test school ",major="  Test major ",role="ADMIN",passwordHash="forged",permissions=new[]{"MANAGE_USERS"}});
            check(created.StatusCode==HttpStatusCode.Created,"profile normalizes whitespace before validation and persists whitelist");
            using var profile = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
            var id = profile.RootElement.GetProperty("id").GetInt32();
            check(profile.RootElement.GetProperty("email").GetString()=="sprint1-owner@example.invalid" && profile.RootElement.GetProperty("fullName").GetString()=="Sprint 1 owner","created profile response uses normalized values");
            using var duplicate = await http.PostAsJsonAsync("/api/interns",new{fullName="Duplicate",email="sprint1-owner@example.invalid",phone="0912345678",school="Test",major="Test"});
            check(duplicate.StatusCode==HttpStatusCode.Conflict,"profile duplicate email returns conflict");
            var owner = await Login("sprint1-owner@example.invalid","Ownership-test-9");
            http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",owner);
            using var file = new MultipartFormDataContent();
            var bytes = new ByteArrayContent("%PDF-1.4\nSprint 1 ownership"u8.ToArray()); bytes.Headers.ContentType=new("application/pdf");file.Add(bytes,"file","owner.pdf");
            using var upload = await http.PutAsync("/api/interns/me/documents/cv",file);
            check(upload.StatusCode==HttpStatusCode.OK,"owner uploads a document before HR email correction");
            http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",hr);
            await using var versionCommand = sql.CreateCommand();
            versionCommand.CommandText = "SELECT Version FROM dbo.InternDocuments WHERE InternId=@id AND Kind='cv'";
            versionCommand.Parameters.AddWithValue("@id",id);
            var version = Convert.ToBase64String((byte[])(await versionCommand.ExecuteScalarAsync())!);
            await permissions.SaveAsync("HR",permissions.GetDefaults("HR").Where(p=>p!=PermissionNames.ApproveDocuments).ToArray(),CancellationToken.None);
            using var blockedReview = await http.PutAsJsonAsync($"/api/document-reviews/{id}/cv/approve",new {comment="",version});
            check(blockedReview.StatusCode==HttpStatusCode.Forbidden,"revoked HR APPROVE cannot process a real pending document");
            await permissions.SaveAsync("HR",permissions.GetDefaults("HR"),CancellationToken.None);
            using var acceptedReview = await http.PutAsJsonAsync($"/api/document-reviews/{id}/cv/approve",new {comment="",version});
            check(acceptedReview.StatusCode==HttpStatusCode.OK,"same HR token processes real pending document after APPROVE regrant");
            using var hijack = await http.PutAsJsonAsync($"/api/interns/{id}",new{fullName="Owner",email="sprint1-other@example.invalid",phone="0912345678",school="Test",major="Test"});
            check(hijack.StatusCode==HttpStatusCode.Conflict,"HR cannot transfer profile to another existing account email");
            using var edited = await http.PutAsJsonAsync($"/api/interns/{id}",new{fullName="Corrected owner",email="sprint1-corrected@example.invalid",phone="0912345678",school="Test",major="Test",status="Đang thực tập",role="ADMIN",passwordHash="forged"});
            check(edited.StatusCode==HttpStatusCode.OK,"HR changes linked email atomically");
            using var filter = await http.GetAsync("/api/interns/search?school=Test&major=Test&status="+Uri.EscapeDataString("Đang thực tập"));
            using var filterData = JsonDocument.Parse(await filter.Content.ReadAsStringAsync());
            check(filter.StatusCode==HttpStatusCode.OK && filterData.RootElement.EnumerateArray().Any(p=>p.GetProperty("id").GetInt32()==id)
                && filterData.RootElement.EnumerateArray().All(p=>p.GetProperty("status").GetString()=="Đang thực tập"),"school/major/status filters return persisted matching profiles");
            using var invalidStatus = await http.GetAsync("/api/interns/search?status=FAKE_STATUS");
            check(invalidStatus.StatusCode==HttpStatusCode.BadRequest,"search rejects invented profile statuses");
            http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",owner);
            using var own = await http.GetAsync("/api/interns/me");
            using var ownData = JsonDocument.Parse(await own.Content.ReadAsStringAsync());
            check(own.StatusCode==HttpStatusCode.OK && ownData.RootElement.GetProperty("id").GetInt32()==id,"existing owner token retains same profile after email edit");
            using var document = await http.GetAsync("/api/interns/me/documents/cv");
            check(document.StatusCode==HttpStatusCode.OK,"existing owner retains document after email edit");
            await using var verify = sql.CreateCommand();
            verify.CommandText="SELECT Username,Role,PasswordHash FROM dbo.Users WHERE Email='sprint1-corrected@example.invalid'";
            await using(var reader = await verify.ExecuteReaderAsync())
            {
                await reader.ReadAsync();
                check(reader.GetString(0)=="sprint1-corrected@example.invalid" && reader.GetString(1)=="INTERN" && reader.GetString(2)==hash,"email login follows correction while role and password hash remain unchanged");
            }
            await permissions.SaveAsync("INTERN",permissions.GetDefaults("INTERN").Where(p=>p!=PermissionNames.UploadDocuments).ToArray(),CancellationToken.None);
            using var read = await http.GetAsync("/api/interns/me/documents");
            using var blockedFile = new MultipartFormDataContent();
            var blockedBytes = new ByteArrayContent("%PDF-1.4\nBlocked replacement"u8.ToArray());
            blockedBytes.Headers.ContentType = new("application/pdf"); blockedFile.Add(blockedBytes,"file","blocked.pdf");
            using var denied = await http.PutAsync("/api/interns/me/documents/cv",blockedFile);
            check(read.StatusCode==HttpStatusCode.OK && denied.StatusCode==HttpStatusCode.Forbidden,"same Intern token can read but cannot upload after CREATE revocation");
        }
        finally
        {
            await permissions.SaveAsync("INTERN",permissions.GetDefaults("INTERN"),CancellationToken.None);
            await permissions.SaveAsync("HR",permissions.GetDefaults("HR"),CancellationToken.None);
            await using var cleanup = sql.CreateCommand();
            cleanup.CommandText = """
                DELETE d FROM dbo.InternDocuments d JOIN dbo.Interns i ON i.Id=d.InternId
                    WHERE i.Email IN ('sprint1-owner@example.invalid','sprint1-corrected@example.invalid');
                DELETE dbo.Interns WHERE Email IN ('sprint1-owner@example.invalid','sprint1-corrected@example.invalid');
                DELETE dbo.Users WHERE Email IN ('sprint1-owner@example.invalid','sprint1-corrected@example.invalid','sprint1-other@example.invalid');
                """;
            await cleanup.ExecuteNonQueryAsync();
            http.DefaultRequestHeaders.Authorization=previous;
        }
    }
}
