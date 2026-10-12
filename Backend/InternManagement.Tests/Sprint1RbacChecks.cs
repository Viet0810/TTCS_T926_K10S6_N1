using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InternManagement.Services;
using Microsoft.Extensions.Configuration;

internal static class Sprint1RbacChecks
{
    public static async Task RunAsync(HttpClient http, string connectionString, Action<bool,string> check, Func<Task> restart, Func<Task> browser)
    {
        var previous = http.DefaultRequestHeaders.Authorization;
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        { ["ConnectionStrings:InternManagement"] = connectionString }).Build();
        var service = new RolePermissionService(config);
        async Task<string> Login(string role)
        {
            using var response = await http.PostAsJsonAsync("/api/auth/login", new { username="permission-"+role.ToLowerInvariant(), password="Permission-test-9" });
            using var data = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return data.RootElement.GetProperty("token").GetString()!;
        }
        var admin = await Login("ADMIN"); var hr = await Login("HR"); var mentor = await Login("MENTOR");
        async Task<HttpStatusCode> Save(string role, IEnumerable<string> permissions)
        {
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",admin);
            using var response = await http.PutAsJsonAsync("/api/permissions/"+role, new {permissions});
            return response.StatusCode;
        }
        try
        {
            await browser();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", hr);
            using var profiles = await http.GetAsync("/api/interns");
            using var profileData = JsonDocument.Parse(await profiles.Content.ReadAsStringAsync());
            var profile = profileData.RootElement[0].Clone();
            var profileId = profile.GetProperty("id").GetInt32();
            check(await Save("HR",service.GetDefaults("HR").Where(p=>p!=PermissionNames.EditInterns))==HttpStatusCode.OK,"Admin revokes HR UPDATE through RBAC API");
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", hr);
            using var blockedUpdate = await http.PutAsJsonAsync($"/api/interns/{profileId}",profile);
            check(blockedUpdate.StatusCode==HttpStatusCode.Forbidden,"same HR token receives 403 on real profile UPDATE after revocation");
            check(await Save("HR",service.GetDefaults("HR"))==HttpStatusCode.OK,"Admin restores HR UPDATE through RBAC API");
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", hr);
            using var allowedUpdate = await http.PutAsJsonAsync($"/api/interns/{profileId}",profile);
            check(allowedUpdate.StatusCode==HttpStatusCode.OK,"same HR token successfully updates existing profile after regrant");
            check(await Save("MENTOR",service.GetDefaults("MENTOR").Append(PermissionNames.EditInterns))==HttpStatusCode.OK,"Admin grants new Mentor UPDATE absent from default mapping");
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mentor);
            using var mentorUpdate = await http.PutAsJsonAsync($"/api/interns/{profileId}",profile);
            check(mentorUpdate.StatusCode==HttpStatusCode.OK,"newly granted Mentor UPDATE is enforced by existing backend architecture");
            check(await Save("MENTOR",service.GetDefaults("MENTOR"))==HttpStatusCode.OK,"Admin removes newly granted Mentor UPDATE");
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mentor);
            using var mentorBlocked = await http.PutAsJsonAsync($"/api/interns/{profileId}",profile);
            check(mentorBlocked.StatusCode==HttpStatusCode.Forbidden,"Mentor cannot bypass revoked UPDATE by calling API directly");
            check(await Save("ADMIN", service.GetDefaults("ADMIN").Where(p=>p!=PermissionNames.ReadPermissions)) == HttpStatusCode.BadRequest, "RBAC cannot revoke essential Admin READ permission");
            check(await Save("ADMIN", service.GetDefaults("ADMIN").Where(p=>p!=PermissionNames.ManagePermissions)) == HttpStatusCode.BadRequest, "RBAC cannot lock out Admin configuration");
            check(await Save("ADMIN", []) == HttpStatusCode.BadRequest, "RBAC cannot remove all essential Admin permissions");
            check(await Save("ROOT", []) == HttpStatusCode.BadRequest, "RBAC rejects unknown roles");
            check(await Save("HR", service.GetDefaults("HR").Append("FAKE_DELETE")) == HttpStatusCode.BadRequest, "RBAC rejects invented action codes");
            check(await Save("HR", service.GetDefaults("HR").Append(PermissionNames.ReadPermissions)) == HttpStatusCode.BadRequest, "RBAC prevents delegating Admin-only RBAC authority");
            check(await Save("HR", service.GetDefaults("HR").Where(p=>p!=PermissionNames.ReadDocumentReviews)) == HttpStatusCode.BadRequest, "RBAC validates action/read dependencies");
            var readOnly = service.GetDefaults("HR").Where(p=>p!=PermissionNames.ApproveDocuments).ToArray();
            check(await Save("HR", readOnly) == HttpStatusCode.OK, "Admin persists HR read-only review grants");
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", hr);
            using var list = await http.GetAsync("/api/document-reviews");
            using var decision = await http.PutAsJsonAsync("/api/document-reviews/1/cv/approve",new{comment="",version="AAAAAAAAAAE="});
            check(list.StatusCode==HttpStatusCode.OK && decision.StatusCode==HttpStatusCode.Forbidden, "same HR token keeps READ and immediately loses APPROVE");
            using var session = await http.GetAsync("/api/auth/me");
            using var data = JsonDocument.Parse(await session.Content.ReadAsStringAsync());
            check(!data.RootElement.GetProperty("permissions").EnumerateArray().Any(p=>p.GetString()==PermissionNames.ApproveDocuments), "session exposes live persisted grants");
            await new DatabaseInitializer().InitializeAsync(connectionString);
            check(!new RolePermissionService(config).HasPermission("HR",PermissionNames.ApproveDocuments), "restart and initialization preserve revocations");
            await restart();
            using var restartDecision = await http.PutAsJsonAsync("/api/document-reviews/1/cv/approve",new{comment="",version="AAAAAAAAAAE="});
            using var restartRead = await http.GetAsync("/api/document-reviews");
            check(restartDecision.StatusCode==HttpStatusCode.Forbidden && restartRead.StatusCode==HttpStatusCode.OK,
                "actual API process restart preserves READ and revoked APPROVE with existing token");
            var newHr = await Login("HR");
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",newHr);
            using var newSession = await http.GetAsync("/api/auth/me");
            using var newSessionData = JsonDocument.Parse(await newSession.Content.ReadAsStringAsync());
            check(!newSessionData.RootElement.GetProperty("permissions").EnumerateArray().Any(p=>p.GetString()==PermissionNames.ApproveDocuments), "fresh login preserves persisted permission revocation");
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin);
            using var defaults = await http.GetAsync("/api/permissions/HR/defaults");
            check(defaults.StatusCode==HttpStatusCode.OK && !service.HasPermission("HR",PermissionNames.ApproveDocuments), "loading backend defaults does not change persisted grants");
            check(await Save("HR",service.GetDefaults("HR")) == HttpStatusCode.OK, "saving defaults restores HR approval");
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",hr);
            using var restored = await http.PutAsJsonAsync("/api/document-reviews/2147483647/cv/approve",new{comment="",version="AAAAAAAAAAE="});
            check(restored.StatusCode==HttpStatusCode.NotFound,"regrant lets same HR token reach authorized review logic");
            var mentorRead = service.GetDefaults("MENTOR").Where(p=>p!=PermissionNames.ViewInterns).ToArray();
            check(await Save("MENTOR",mentorRead)==HttpStatusCode.OK,"Admin revokes Mentor profile READ independently of search");
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",mentor);
            using var denied = await http.GetAsync("/api/interns");
            using var search = await http.GetAsync("/api/interns/search");
            using var forbiddenSave = await http.PutAsJsonAsync("/api/permissions/HR",new{permissions=service.GetDefaults("HR")});
            check(denied.StatusCode==HttpStatusCode.Forbidden && search.StatusCode==HttpStatusCode.OK,"READ revocation is enforced while independent SEARCH remains available");
            check(forbiddenSave.StatusCode==HttpStatusCode.Forbidden,"Mentor cannot modify RBAC with a forged API request");
        }
        finally
        {
            await service.SaveAsync("HR",service.GetDefaults("HR"),CancellationToken.None);
            await service.SaveAsync("MENTOR",service.GetDefaults("MENTOR"),CancellationToken.None);
            await service.SaveAsync("INTERN",service.GetDefaults("INTERN"),CancellationToken.None);
            http.DefaultRequestHeaders.Authorization = previous;
        }
    }
}
