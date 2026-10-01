using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;

internal static class DocumentChecks
{
    public static async Task RunAsync(HttpClient http,string connectionString,Action<bool,string> check)
    {
        async Task<HttpResponseMessage> Upload(string kind,byte[] bytes,string name="cv.pdf", string? contentType="application/pdf") {
            using var body=new MultipartFormDataContent();
            var content=new ByteArrayContent(bytes);
            if(contentType is not null) content.Headers.ContentType=new MediaTypeHeaderValue(contentType);
            body.Add(content,"file",name);
            return await http.PutAsync("/api/interns/me/documents/"+kind,body);
        }
        var pdf=Encoding.ASCII.GetBytes("%PDF-1.4\nIntegration document\n%%EOF");
        check((await http.GetAsync("/api/interns/me/documents")).StatusCode==HttpStatusCode.Unauthorized,"anonymous document access is rejected");
        check((await Upload("cv",pdf)).StatusCode==HttpStatusCode.Unauthorized,"anonymous direct upload is rejected");
        async Task Login(string role) {
            using var response=await http.PostAsJsonAsync("/api/auth/login",new{username="permission-"+role,password="Permission-test-9"});
            using var data=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",data.RootElement.GetProperty("token").GetString());
        }
        await Login("hr");
        check((await http.GetAsync("/api/interns/me/documents")).StatusCode==HttpStatusCode.Forbidden,"HR cannot use intern-only upload endpoints");
        check((await Upload("cv",pdf)).StatusCode==HttpStatusCode.Forbidden,"HR direct upload is rejected");
        await Login("admin");
        check((await Upload("cv",pdf)).StatusCode==HttpStatusCode.Forbidden,"admin direct upload is rejected");
        await Login("mentor");
        check((await Upload("cv",pdf)).StatusCode==HttpStatusCode.Forbidden,"mentor direct upload is rejected");
        await Login("intern");
        check((await http.GetAsync("/api/interns/me/documents")).StatusCode==HttpStatusCode.NotFound,"intern without linked profile receives a useful error");
        check((await Upload("cv",pdf)).StatusCode==HttpStatusCode.NotFound,"upload requires a linked profile");
        await using var sql=new SqlConnection(connectionString); await sql.OpenAsync();
        await using var link=sql.CreateCommand();
        link.CommandText="UPDATE dbo.Users SET Email=(SELECT TOP(1) Email FROM dbo.Interns ORDER BY Id) WHERE Username='permission-intern'";
        await link.ExecuteNonQueryAsync();
        using(var emptyForm=new MultipartFormDataContent())
            check((await http.PutAsync("/api/interns/me/documents/cv",emptyForm)).StatusCode==HttpStatusCode.BadRequest,"missing file is rejected");
        check((await Upload("cv",[])).StatusCode==HttpStatusCode.BadRequest,"empty file is rejected");
        check((await Upload("cv",pdf,contentType:"text/html")).StatusCode==HttpStatusCode.BadRequest,"incompatible content type is rejected");
        check((await Upload("cv",Encoding.ASCII.GetBytes("fake"))).StatusCode==HttpStatusCode.BadRequest,"fake PDF contents are rejected");
        check((await Upload("cv",pdf,"cv.exe")).StatusCode==HttpStatusCode.BadRequest,"unsupported document extension is rejected");
        check((await Upload("unknown",pdf)).StatusCode==HttpStatusCode.BadRequest,"unsupported document kind is rejected");
        check((await Upload("cv",pdf)).IsSuccessStatusCode,"CV uploads through multipart API");
        check((await Upload("application",pdf,"application.pdf")).IsSuccessStatusCode,"internship application uploads through multipart API");
        var second=Encoding.ASCII.GetBytes("%PDF-1.7\nReplacement\n%%EOF");
        check((await Upload("cv",second)).IsSuccessStatusCode,"same filename safely replaces only the owned CV");
        check((await http.GetByteArrayAsync("/api/interns/me/documents/application")).SequenceEqual(pdf),"same-name replacement leaves the application unchanged");
        check((await Upload("cv",pdf,"../../outside.pdf")).IsSuccessStatusCode,"client path is stripped from the display filename");
        using(var sanitized=JsonDocument.Parse(await http.GetStringAsync("/api/interns/me/documents")))
            check(sanitized.RootElement.EnumerateArray().Any(item=>item.GetProperty("fileName").GetString()=="outside.pdf"),"stored filename contains no client path");
        check((await Upload("cv",second,"updated.pdf")).IsSuccessStatusCode,"resubmitting replaces the CV");
        using var download=await http.GetAsync("/api/interns/me/documents/cv");
        check((await download.Content.ReadAsByteArrayAsync()).SequenceEqual(second),"download returns exactly the persisted document bytes");
        check(download.Content.Headers.ContentDisposition?.DispositionType=="attachment","document downloads use attachments");
        using var list=JsonDocument.Parse(await http.GetStringAsync("/api/interns/me/documents"));
        check(list.RootElement.GetArrayLength()==2,"replacement keeps exactly one document per kind");
        // Inject a storage failure only in the disposable integration database.
        await using(var fault=sql.CreateCommand()) {
            fault.CommandText="CREATE TRIGGER dbo.DocumentUploadTestFailure ON dbo.InternDocuments AFTER INSERT, UPDATE AS THROW 51000, 'Private storage failure detail', 1;";
            await fault.ExecuteNonQueryAsync();
        }
        try {
            using var failed=await Upload("cv",pdf);
            var payload=await failed.Content.ReadAsStringAsync();
            using var error=JsonDocument.Parse(payload);
            check(failed.StatusCode==HttpStatusCode.InternalServerError && error.RootElement.GetProperty("message").GetString()!.Contains("Vui lòng thử lại")
                && !payload.Contains("Private storage") && !payload.Contains("SqlException"),"storage errors return safe JSON feedback");
            check((await http.GetByteArrayAsync("/api/interns/me/documents/cv")).SequenceEqual(second),"failed save preserves the previous document");
        }
        finally {
            await using var restore=sql.CreateCommand(); restore.CommandText="DROP TRIGGER dbo.DocumentUploadTestFailure"; await restore.ExecuteNonQueryAsync();
        }
        check((await http.GetAsync("/api/document-reviews")).StatusCode==HttpStatusCode.Forbidden,"intern cannot access HR review queue");
        var missingRoute="/api/document-reviews/2147483647/cv";
        var decision=new {status="approved",comment="",version="AAAAAAAAAAA="};
        check((await http.PutAsJsonAsync(missingRoute,decision)).StatusCode==HttpStatusCode.Forbidden,"intern direct review is forbidden");
        await Login("mentor");
        check((await http.GetAsync("/api/document-reviews")).StatusCode==HttpStatusCode.Forbidden,"mentor cannot review documents");
        check((await http.PutAsJsonAsync(missingRoute,decision)).StatusCode==HttpStatusCode.Forbidden,"mentor direct review is forbidden");
        http.DefaultRequestHeaders.Authorization=null;
        check((await http.PutAsJsonAsync(missingRoute,decision)).StatusCode==HttpStatusCode.Unauthorized,"anonymous direct review is unauthorized");
        await Login("hr");
        using var queue=JsonDocument.Parse(await http.GetStringAsync("/api/document-reviews"));
        var cv=queue.RootElement.EnumerateArray().First(item=>item.GetProperty("kind").GetString()=="cv");
        var route=$"/api/document-reviews/{cv.GetProperty("internId").GetInt32()}/cv";
        var version=cv.GetProperty("version").GetString();
        check(queue.RootElement.GetArrayLength()==2 && queue.RootElement.EnumerateArray().All(item=>item.GetProperty("status").GetString()=="pending"),"HR sees both uploaded documents pending");
        check((await http.GetAsync(missingRoute)).StatusCode==HttpStatusCode.NotFound,"missing file returns 404 on download");
        check((await http.PutAsJsonAsync(missingRoute,decision)).StatusCode==HttpStatusCode.NotFound,"missing document returns 404 on review");
        check((await http.PutAsJsonAsync(route,new {status="invalid",version})).StatusCode==HttpStatusCode.BadRequest,"invalid review status is rejected");
        check((await http.PutAsJsonAsync(route,new {})).StatusCode==HttpStatusCode.BadRequest,"missing review data is rejected");
        check((await http.PutAsJsonAsync(route,new {status="approved",version="bad"})).StatusCode==HttpStatusCode.BadRequest,"invalid rowversion is rejected");
        check((await http.PutAsJsonAsync(route.Replace("/cv","/unknown"),decision)).StatusCode==HttpStatusCode.BadRequest,"invalid review kind is rejected");
        check((await http.GetAsync(route)).IsSuccessStatusCode,"HR can download submitted documents");
        check((await http.PutAsJsonAsync(route,new{status="rejected",comment="",version})).StatusCode==HttpStatusCode.BadRequest,"rejection requires a reason");
        check((await http.PutAsJsonAsync(route,new{status="approved",comment="Complete",version})).IsSuccessStatusCode,"HR can approve the current version");
        check((await http.PutAsJsonAsync(route,new{status="rejected",comment="Stale",version})).StatusCode==HttpStatusCode.Conflict,"stale review cannot overwrite a newer decision");
        async Task<string> CurrentVersion() {
            using var refreshed=JsonDocument.Parse(await http.GetStringAsync("/api/document-reviews"));
            return refreshed.RootElement.EnumerateArray().First(item=>item.GetProperty("kind").GetString()=="cv").GetProperty("version").GetString()!;
        }
        version=await CurrentVersion();
        check((await http.PutAsJsonAsync(route,new{status="approved",version})).StatusCode==HttpStatusCode.Conflict,"already approved document rejects repeat approval");
        await using(var fault=sql.CreateCommand()) {
            fault.CommandText="CREATE TRIGGER dbo.DocumentReviewTestFailure ON dbo.InternDocuments AFTER UPDATE AS THROW 51000, 'Private review failure', 1;";
            await fault.ExecuteNonQueryAsync();
        }
        try {
            using var failure=await http.PutAsJsonAsync(route,new{status="rejected",comment="Missing details",version});
            var payload=await failure.Content.ReadAsStringAsync(); using var error=JsonDocument.Parse(payload);
            check(failure.StatusCode==HttpStatusCode.InternalServerError && error.RootElement.TryGetProperty("message",out _) && !payload.Contains("Private review"),"failed review returns safe JSON");
            check(await CurrentVersion()==version,"failed review preserves previous status and version");
        } finally {
            await using var restore=sql.CreateCommand();restore.CommandText="DROP TRIGGER dbo.DocumentReviewTestFailure";await restore.ExecuteNonQueryAsync();
        }
        check((await http.PutAsJsonAsync(route,new{status="rejected",comment="Missing details",version})).IsSuccessStatusCode,"HR rejection is persisted");
        version=await CurrentVersion();
        check((await http.PutAsJsonAsync(route,new{status="rejected",comment="Missing details",version})).StatusCode==HttpStatusCode.Conflict,"already rejected document rejects repeat rejection");
        using(var persisted=JsonDocument.Parse(await http.GetStringAsync("/api/document-reviews")))
            check(persisted.RootElement.EnumerateArray().Any(item=>item.GetProperty("status").GetString()=="rejected" && item.GetProperty("comment").GetString()=="Missing details"),"reload returns persisted rejection and reason");
        check((await http.PutAsJsonAsync(route,new{status="approved",comment="Complete",version})).IsSuccessStatusCode,"HR can change a rejection to approval");
        await Login("intern");
        using var approved=JsonDocument.Parse(await http.GetStringAsync("/api/interns/me/documents"));
        check(approved.RootElement.EnumerateArray().Any(item=>item.GetProperty("status").GetString()=="approved"),"intern sees persisted approval status");
        await Upload("cv",pdf);
        using var reset=JsonDocument.Parse(await http.GetStringAsync("/api/interns/me/documents"));
        check(reset.RootElement.EnumerateArray().All(item=>item.GetProperty("status").GetString()=="pending"),"resubmitting resets the review to pending");
        var oversized=new byte[5*1024*1024+1]; pdf.CopyTo(oversized,0);
        var tooBig=await Upload("cv",oversized);
        check(tooBig.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge,"oversized documents are rejected");
        await Login("admin");
        check((await http.GetAsync("/api/document-reviews")).IsSuccessStatusCode,"admin retains existing review access");
        var applicationRoute=route.Replace("/cv","/application");
        await using(var remove=sql.CreateCommand()) {
            remove.CommandText="DELETE FROM dbo.InternDocuments WHERE InternId=@id AND Kind='application'";
            remove.Parameters.AddWithValue("@id",cv.GetProperty("internId").GetInt32());await remove.ExecuteNonQueryAsync();
        }
        await Login("hr");
        check((await http.GetAsync(applicationRoute)).StatusCode==HttpStatusCode.NotFound,"file deleted after listing returns 404");
        check((await http.PutAsJsonAsync(applicationRoute,decision)).StatusCode==HttpStatusCode.NotFound,"deleted document cannot be reviewed");
        await Login("intern");
        await using var unlink=sql.CreateCommand(); unlink.CommandText="UPDATE dbo.Users SET Email='unlinked@example.invalid' WHERE Username='permission-intern'"; await unlink.ExecuteNonQueryAsync();
        check((await http.GetAsync("/api/interns/me/documents/cv")).StatusCode==HttpStatusCode.NotFound,"document access follows current profile ownership");
        http.DefaultRequestHeaders.Authorization=null;
    }
}
