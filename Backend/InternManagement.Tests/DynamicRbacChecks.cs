using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InternManagement.Services;
using Microsoft.Extensions.Configuration;

internal static class DynamicRbacChecks
{
    public static async Task RunAsync(HttpClient http,string connectionString,Action<bool,string> check)
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["ConnectionStrings:InternManagement"]=connectionString}).Build();
        var permissions=new RolePermissionService(config);var original=permissions.GetRolePermissions();var previous=http.DefaultRequestHeaders.Authorization;
        async Task<string> Login(string role){using var response=await http.PostAsJsonAsync("/api/auth/login",new{username="permission-"+role.ToLowerInvariant(),password="Permission-test-9"});response.EnsureSuccessStatusCode();using var data=JsonDocument.Parse(await response.Content.ReadAsStringAsync());return data.RootElement.GetProperty("token").GetString()!;}
        var admin=await Login("ADMIN");var mentor=await Login("MENTOR");var hr=await Login("HR");var intern=await Login("INTERN");
        void Use(string token)=>http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);
        async Task Save(string role,IEnumerable<string> grants){Use(admin);using var response=await http.PutAsJsonAsync("/api/permissions/"+role,new{permissions=grants});check(response.StatusCode==HttpStatusCode.OK,"Dynamic Admin persists "+role+" grants");}
        async Task Status(HttpResponseMessage response,HttpStatusCode expected,string name){using(response){check(response.StatusCode==expected,name+" ("+(int)response.StatusCode+")");}await Task.CompletedTask;}
        int? profileId=null,programId=null;
        try
        {
            check(permissions.GetCatalog().Count==PermissionCatalog.Entries.Length && permissions.GetCatalog().All(p=>p.Editable),"permission definitions stored in SQL and every business grant is editable");
            var grants=original["MENTOR"].Concat(new[]{PermissionNames.ManagePrograms,PermissionNames.CreateProgram,PermissionNames.UpdateProgram,PermissionNames.DeleteProgram,
                PermissionNames.ReadDocumentReviews,PermissionNames.ApproveDocuments,PermissionNames.ManageContracts,PermissionNames.CreateContract,PermissionNames.UpdateContract,PermissionNames.DeleteContract,
                PermissionNames.AssignMentor,PermissionNames.CreateAssignment,PermissionNames.UpdateAssignment,PermissionNames.DeleteAssignment}).Distinct().ToArray();
            await Save("MENTOR",grants);Use(mentor);
            await Status(await http.GetAsync("/api/document-reviews"),HttpStatusCode.OK,"Mentor uses newly granted review READ without HR actor restriction");
            await Status(await http.GetAsync("/api/contracts/interns"),HttpStatusCode.OK,"contract options require contract READ, not unrelated profile permissions");
            using(var created=await http.PostAsJsonAsync("/api/program-schedule",new{startDate="2026-10-01",endDate="2026-10-31",name="Dynamic RBAC"}))
            {check(created.StatusCode==HttpStatusCode.OK,"Mentor creates program via granted CREATE");using var data=JsonDocument.Parse(await created.Content.ReadAsStringAsync());programId=data.RootElement.GetProperty("data").GetProperty("id").GetInt32();}
            await Status(await http.PutAsJsonAsync("/api/program-schedule/"+programId,new{startDate="2026-10-02",endDate="2026-10-30",name="Dynamic edited"}),HttpStatusCode.OK,"Mentor updates program via granted UPDATE");
            await Save("MENTOR",grants.Where(p=>p!=PermissionNames.DeleteProgram));Use(mentor);
            await Status(await http.DeleteAsync("/api/program-schedule/"+programId),HttpStatusCode.Forbidden,"revoked program DELETE blocks direct API with existing token");
            await Status(await http.GetAsync("/api/program-schedule"),HttpStatusCode.OK,"program READ remains when DELETE revoked");
            await Save("MENTOR",grants);Use(hr);
            using(var created=await http.PostAsJsonAsync("/api/interns",new{fullName="Dynamic RBAC intern",email="permission-intern@example.invalid",phone="0912345678",school="Test",major="Test"}))
            {check(created.StatusCode==HttpStatusCode.Created,"isolated dynamic RBAC profile created");using var data=JsonDocument.Parse(await created.Content.ReadAsStringAsync());profileId=data.RootElement.GetProperty("id").GetInt32();}
            await Sprint2Fixtures.ApprovedAsync(connectionString,profileId.Value);
            MultipartFormDataContent File(){var form=new MultipartFormDataContent();var content=new ByteArrayContent("%PDF-1.4\nDynamic RBAC"u8.ToArray());content.Headers.ContentType=new("application/pdf");form.Add(content,"file","dynamic.pdf");return form;}
            Use(mentor);using(var file=File())await Status(await http.PutAsync("/api/contracts/"+profileId,file),HttpStatusCode.OK,"Mentor creates contract via CREATE permission");
            await Save("MENTOR",grants.Where(p=>p!=PermissionNames.UpdateContract));Use(mentor);using(var file=File())await Status(await http.PutAsync("/api/contracts/"+profileId,file),HttpStatusCode.Forbidden,"CREATE cannot overwrite an existing contract without UPDATE");
            await Save("MENTOR",grants.Where(p=>p!=PermissionNames.CreateContract));Use(mentor);using(var file=File())await Status(await http.PutAsync("/api/contracts/"+profileId,file),HttpStatusCode.OK,"UPDATE can replace existing unconfirmed contract without CREATE");
            await Save("MENTOR",grants.Where(p=>p!=PermissionNames.DeleteContract));Use(mentor);
            await Status(await http.DeleteAsync("/api/contracts/"+profileId),HttpStatusCode.Forbidden,"contract DELETE is enforced independently of READ/UPDATE");
            await Save("MENTOR",grants);Use(mentor);
            await Status(await http.DeleteAsync("/api/contracts/"+profileId),HttpStatusCode.OK,"regranted contract DELETE deletes real SQL record");
            Use(mentor);using(var file=File())await Status(await http.PutAsync("/api/contracts/"+profileId,file),HttpStatusCode.OK,"contract prepared for assignment lifecycle");
            var mentorId=(await new AccountService(config,new PasswordHasher()).GetAllAsync(default)).First(u=>u.Username=="permission-mentor").Id;
            await Status(await http.PutAsJsonAsync("/api/intern-assignments/"+profileId,new{mentorUserId=mentorId,internshipProgramId=programId}),HttpStatusCode.OK,"Mentor creates assignment via granted permission");
            await Save("MENTOR",grants.Where(p=>p!=PermissionNames.UpdateAssignment));Use(mentor);
            await Status(await http.PutAsJsonAsync("/api/intern-assignments/"+profileId,new{mentorUserId=mentorId,internshipProgramId=programId}),HttpStatusCode.Forbidden,"assignment CREATE cannot bypass UPDATE guard on assigned record");
            await Save("MENTOR",grants);Use(mentor);
            await Status(await http.DeleteAsync("/api/intern-assignments/"+profileId),HttpStatusCode.OK,"Mentor deletes assignment via granted DELETE");
            await Save("INTERN",original["INTERN"].Append(PermissionNames.ManagePrograms));Use(intern);
            await Status(await http.GetAsync("/api/program-schedule"),HttpStatusCode.OK,"Intern can use explicitly granted program READ");
            await Status(await http.PostAsJsonAsync("/api/program-schedule",new{startDate="2026-10-01",endDate="2026-10-31"}),HttpStatusCode.Forbidden,"Intern READ does not grant CREATE");
            await Save("INTERN",original["INTERN"]);Use(intern);
            await Status(await http.GetAsync("/api/program-schedule"),HttpStatusCode.Forbidden,"Intern loses program access immediately after revocation");
            await Save("INTERN",original["INTERN"].Where(p=>p!=PermissionNames.EditOwnProfile));Use(intern);
            await Status(await http.GetAsync("/api/interns/me"),HttpStatusCode.OK,"own profile READ remains after revoking UPDATE");
            var ownPayload=new{fullName="Dynamic RBAC intern",phone="0912345678",school="Test",major="Test"};
            await Status(await http.PutAsJsonAsync("/api/interns/me",ownPayload),HttpStatusCode.Forbidden,"own profile UPDATE requires its distinct permission");
            await Save("INTERN",original["INTERN"]);Use(intern);
            await Status(await http.PutAsJsonAsync("/api/interns/me",ownPayload),HttpStatusCode.OK,"own profile UPDATE regrant works with existing token");
            await Save("MENTOR",original["MENTOR"].Where(p=>p!=PermissionNames.ViewProfile));Use(mentor);
            await Status(await http.GetAsync("/api/account/profile"),HttpStatusCode.Forbidden,"account profile READ permission enforced by dedicated API");
            await Save("MENTOR",original["MENTOR"]);Use(mentor);
            await Status(await http.GetAsync("/api/account/profile"),HttpStatusCode.OK,"account profile READ regrant applies to existing token");
            await Save("HR",original["HR"].Append(PermissionNames.DeleteContract));Use(hr);
            await Status(await http.DeleteAsync("/api/contracts/2147483647"),HttpStatusCode.NotFound,"Admin can enable HR CONTRACT_DELETE absent from defaults");
            await Save("HR",original["HR"]);Use(hr);
            await Status(await http.DeleteAsync("/api/contracts/2147483647"),HttpStatusCode.Forbidden,"Admin removes HR CONTRACT_DELETE independently");
            await new DatabaseInitializer().InitializeAsync(connectionString);
            check(!permissions.HasPermission("HR",PermissionNames.DeleteContract),"migration restart never restores revoked newly added grant");
        }
        finally
        {
            foreach(var role in original.Keys)await permissions.SaveAsync(role,original[role],default);
            var sql=new Microsoft.Data.SqlClient.SqlConnection(connectionString);await using(sql){await sql.OpenAsync();await using var command=sql.CreateCommand();command.CommandText="DELETE dbo.InternContracts WHERE InternId=@id; DELETE dbo.Interns WHERE Id=@id; DELETE dbo.InternshipPrograms WHERE Id=@program";command.Parameters.AddWithValue("@id",(object?)profileId??DBNull.Value);command.Parameters.AddWithValue("@program",(object?)programId??DBNull.Value);await command.ExecuteNonQueryAsync();}
            http.DefaultRequestHeaders.Authorization=previous;
        }
    }
}
