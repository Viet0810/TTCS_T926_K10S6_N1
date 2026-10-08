using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using InternManagement.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

internal static class Sprint2CompletionChecks
{
    public static async Task RunAsync(HttpClient http,string connectionString,Action<bool,string> check)
    {
        async Task Login(string username,string password="Permission-test-9")
        {
            http.DefaultRequestHeaders.Authorization=null;
            using var response=await http.PostAsJsonAsync("/api/auth/login",new{username,password});check(response.IsSuccessStatusCode,"Sprint 2 login "+username);
            using var data=JsonDocument.Parse(await response.Content.ReadAsStringAsync());http.DefaultRequestHeaders.Authorization=new("Bearer",data.RootElement.GetProperty("token").GetString());
        }
        async Task<JsonElement> Get(string url)
        {
            using var response=await http.GetAsync(url);check(response.IsSuccessStatusCode,"Sprint 2 GET "+url);
            var text=await response.Content.ReadAsStringAsync();return string.IsNullOrEmpty(text)?JsonDocument.Parse("null").RootElement.Clone():JsonDocument.Parse(text).RootElement.Clone();
        }
        async Task<HttpResponseMessage> Upload(string route,string content="%PDF-1.4 contract",string name="contract.pdf")
        {
            using var form=new MultipartFormDataContent();var bytes=new ByteArrayContent(Encoding.UTF8.GetBytes(content));bytes.Headers.ContentType=new("application/pdf");form.Add(bytes,"file",name);
            return await http.PutAsync(route,form);
        }
        http.DefaultRequestHeaders.Authorization=null;
        using(var anonymous=await http.GetAsync("/api/contracts"))check(anonymous.StatusCode==HttpStatusCode.Unauthorized,"contracts reject anonymous");
        using(var anonymous=await http.PostAsync("/api/interns/me/attendance/check-in",null))check(anonymous.StatusCode==HttpStatusCode.Unauthorized,"check-in rejects anonymous");
        const string email="sprint2-completion@ictu.edu.vn",password="Sprint2-test-9";
        using(var registered=await http.PostAsJsonAsync("/api/auth/register",new{fullName="Sprint 2 workflow",email,password,phone="0912345678",school="ICTU",major="IT"}))check(registered.StatusCode==HttpStatusCode.Created,"US6 registers durable intern account/profile");
        await Login(email,password);
        var profile=await Get("/api/interns/me");var owner=profile.GetProperty("id").GetInt32();
        var ownUpdate=new{fullName="  Updated intern  ",phone=" 0987654321 ",school=" ICTU ",major=" Software ",address=" Hanoi "};
        http.DefaultRequestHeaders.Authorization=null;
        using(var denied=await http.PutAsJsonAsync("/api/interns/me",ownUpdate))check(denied.StatusCode==HttpStatusCode.Unauthorized,"own profile requires authentication");
        foreach(var role in new[]{"permission-admin","permission-hr","permission-mentor"})
        {
            await Login(role);
            using var denied=await http.PutAsJsonAsync("/api/interns/me",ownUpdate);
            check(denied.StatusCode==HttpStatusCode.Forbidden,"own profile rejects "+role);
        }
        await Login(email,password);
        foreach(var field in new[]{"userId","internId","email","role","status","mentorUserId","internshipProgramId","startDate","endDate"})
        {
            var payload=new Dictionary<string,object>{{"fullName","Updated intern"},{"phone","0987654321"},{"school","ICTU"},{"major","Software"},{field,"forbidden"}};
            using var denied=await http.PutAsJsonAsync("/api/interns/me",payload);
            check(denied.StatusCode==HttpStatusCode.BadRequest,"own profile rejects unexpected field "+field);
        }
        foreach(var phone in new[]{"","0123456789","09123456789","invalid"})
        {
            using var invalid=await http.PutAsJsonAsync("/api/interns/me",new{fullName="Intern",phone,school="ICTU",major="IT"});
            check(invalid.StatusCode==HttpStatusCode.BadRequest,"own profile rejects invalid phone");
        }
        using(var invalid=await http.PutAsJsonAsync("/api/interns/me",new{fullName="  ",phone="0987654321",school="ICTU",major="IT"}))check(invalid.StatusCode==HttpStatusCode.BadRequest,"own profile rejects whitespace name");
        using(var invalid=await http.PutAsJsonAsync("/api/interns/me",new{fullName=new string('x',201),phone="0987654321",school="ICTU",major="IT"}))check(invalid.StatusCode==HttpStatusCode.BadRequest,"own profile enforces field length");
        using(var updated=await http.PutAsJsonAsync("/api/interns/me?internId=2147483647",ownUpdate))check(updated.IsSuccessStatusCode,"own profile saves using token ownership");
        var saved=await Get("/api/interns/me");
        check(saved.GetProperty("fullName").GetString()=="Updated intern"&&saved.GetProperty("phone").GetString()=="0987654321"&&saved.GetProperty("address").GetString()=="Hanoi","own profile trims and persists");
        foreach(var field in profile.EnumerateObject().Where(p=>!new[]{"fullName","phone","school","major","address"}.Contains(p.Name)))
            check(saved.GetProperty(field.Name).GetRawText()==field.Value.GetRawText(),"own profile preserves "+field.Name);
        var account=await Get("/api/auth/me");check(account.GetProperty("user").GetProperty("fullName").GetString()=="Updated intern","account name synchronized without invalidating session");
        using(var denied=await http.PutAsJsonAsync($"/api/interns/{owner}",new{fullName="Intruder",email,phone="0987654321",school="ICTU",major="IT"}))check(denied.StatusCode==HttpStatusCode.Forbidden,"intern cannot use administrative profile route");
        await Login("schedule-b@ictu.edu.vn","Schedule-test-9");
        var otherBefore=await Get("/api/interns/me");
        using(var updated=await http.PutAsJsonAsync($"/api/interns/me?internId={owner}",ownUpdate))check(updated.IsSuccessStatusCode,"client owner override cannot select another intern");
        var otherAfter=await Get("/api/interns/me");check(otherAfter.GetProperty("id").GetInt32()==otherBefore.GetProperty("id").GetInt32()&&otherAfter.GetProperty("email").GetString()==otherBefore.GetProperty("email").GetString(),"cross-owner request remains own profile");
        await Login(email,password);
        check((await Get("/api/interns/me")).GetRawText()==saved.GetRawText(),"another intern cannot alter owner profile");
        using(var upload=await Upload("/api/interns/me/documents/cv"))check(upload.IsSuccessStatusCode,"US6 uploads real PDF");
        using(var upload=await Upload("/api/interns/me/documents/application"))check(upload.IsSuccessStatusCode,"US6 uploads real application PDF");
        await Login("permission-hr");
        var queue=await Get("/api/document-reviews");
        foreach(var doc in queue.EnumerateArray().Where(d=>d.GetProperty("internId").GetInt32()==owner))
        {
            using var approved=await http.PutAsJsonAsync($"/api/document-reviews/{owner}/{doc.GetProperty("kind").GetString()}/approve",new{version=doc.GetProperty("version").GetString(),comment="Accepted"});
            check(approved.IsSuccessStatusCode,"US7 approval persists before US8 email attempt");
            var decision=JsonDocument.Parse(await approved.Content.ReadAsStringAsync()).RootElement;
            check(!decision.GetProperty("emailSent").GetBoolean(),"US8 missing SMTP reports delivery failure without undoing review (SMTP success covered by capture tests)");
        }
        using(var bad=await Upload($"/api/contracts/{owner}","not-pdf"))check(bad.StatusCode==HttpStatusCode.BadRequest,"contract validates PDF signature");
        using(var bad=await Upload($"/api/contracts/{owner}","%PDF-", "contract.txt"))check(bad.StatusCode==HttpStatusCode.BadRequest,"contract validates extension");
        using(var upload=await Upload($"/api/contracts/{owner}"))check(upload.IsSuccessStatusCode,"US9 HR uploads contract to SQL");
        var list=await Get("/api/contracts");var oldVersion=list.EnumerateArray().Single(i=>i.GetProperty("internId").GetInt32()==owner).GetProperty("version").GetString();
        using(var replacement=await Upload($"/api/contracts/{owner}","%PDF-1.7 replacement"))check(replacement.IsSuccessStatusCode,"HR can replace unconfirmed contract");
        await Login(email,password);
        using(var wrongRole=await http.GetAsync("/api/contracts"))check(wrongRole.StatusCode==HttpStatusCode.Forbidden,"INTERN cannot manage HR contracts");
        var own=await Get("/api/interns/me/contract?internId=2147483647");check(own.GetProperty("internId").GetInt32()==owner,"own contract ignores client owner override");
        using(var bytes=await http.GetAsync("/api/interns/me/contract/file"))check(await bytes.Content.ReadAsStringAsync()=="%PDF-1.7 replacement","US10 downloads exact own persisted contract");
        using(var stale=await http.PutAsJsonAsync("/api/interns/me/contract/confirm",new{version=oldVersion}))check(stale.StatusCode==HttpStatusCode.Conflict,"stale contract cannot be confirmed");
        using(var invalid=await http.PutAsJsonAsync("/api/interns/me/contract/confirm",new{version="invalid"}))check(invalid.StatusCode==HttpStatusCode.BadRequest,"invalid contract version rejected");
        var confirmPayload=new{version=own.GetProperty("version").GetString()};
        var confirmations=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>http.PutAsJsonAsync("/api/interns/me/contract/confirm",confirmPayload)));
        check(confirmations.Count(r=>r.StatusCode==HttpStatusCode.OK)==1&&confirmations.Count(r=>r.StatusCode==HttpStatusCode.Conflict)==1,"concurrent confirmation succeeds exactly once");foreach(var response in confirmations)response.Dispose();
        own=await Get("/api/interns/me/contract");check(own.GetProperty("confirmedAt").ValueKind==JsonValueKind.String,"US10 confirmation timestamp persists");
        await Login("schedule-b@ictu.edu.vn","Schedule-test-9");
        using(var other=await http.GetAsync($"/api/contracts/{owner}/file"))check(other.StatusCode==HttpStatusCode.Forbidden,"INTERN cannot download another contract via HR route");
        var empty=await Get("/api/interns/me/contract");check(empty.ValueKind==JsonValueKind.Null,"another intern cannot see owner contract");
        using(var other=await http.PutAsJsonAsync("/api/interns/me/contract/confirm",confirmPayload))check(other.StatusCode==HttpStatusCode.Conflict,"another intern cannot confirm owner contract");
        await Login("permission-hr");
        using(var locked=await Upload($"/api/contracts/{owner}"))check(locked.StatusCode==HttpStatusCode.BadRequest,"confirmed contract cannot be overwritten");
        using var created=await http.PostAsJsonAsync("/api/program-schedule",new{name="Sprint 2 program",department="Engineering",startDate="2026-10-01",endDate="2026-12-31"});
        check(created.IsSuccessStatusCode,"US11 creates named department program");var program=JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("data");var programId=program.GetProperty("id").GetInt32();
        var programs=await Get("/api/program-schedule");check(programs.GetProperty("data").EnumerateArray().Any(p=>p.GetProperty("id").GetInt32()==programId&&p.GetProperty("department").GetString()=="Engineering"),"US11/13 program department/dates survive reload");
        var mentors=await Get("/api/intern-assignments/mentors");var mentor=mentors[0].GetProperty("id").GetInt32();
        using(var assigned=await http.PutAsJsonAsync($"/api/intern-assignments/{owner}",new{mentorUserId=mentor,internshipProgramId=programId}))check(assigned.IsSuccessStatusCode,"US12 assigns new intern to mentor/program");
        await Login(email,password);
        var schedule=await Get("/api/interns/me/schedule");check(schedule.GetProperty("departmentName").GetString()=="Engineering"&&schedule.GetProperty("startDate").GetString()=="2026-10-01","US14 sees real assigned program dates/department");
        using(var noIn=await http.PostAsync("/api/interns/me/attendance/check-out",null))check(noIn.StatusCode==HttpStatusCode.Conflict,"check-out before check-in rejected");
        var attempts=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>http.PostAsync("/api/interns/me/attendance/check-in",null)));
        check(attempts.Count(r=>r.StatusCode==HttpStatusCode.OK)==1&&attempts.Count(r=>r.StatusCode==HttpStatusCode.Conflict)==1,"concurrent check-in stores one record");foreach(var response in attempts)response.Dispose();
        using(var checkOut=await http.PostAsync("/api/interns/me/attendance/check-out",null))check(checkOut.IsSuccessStatusCode,"US21 checks out current own record");
        using(var duplicate=await http.PostAsync("/api/interns/me/attendance/check-out",null))check(duplicate.StatusCode==HttpStatusCode.Conflict,"duplicate check-out rejected");
        var today=await Get("/api/interns/me/attendance?internId=2147483647");check(today.GetProperty("checkIn").ValueKind==JsonValueKind.String&&today.GetProperty("checkOut").ValueKind==JsonValueKind.String,"own attendance reload returns durable timestamps");
        await Login("permission-hr");var report=await Get($"/api/attendance/report?internId={owner}&search=Engineering");check(report.GetProperty("records").GetArrayLength()==1&&report.GetProperty("records")[0].GetProperty("department").GetString()=="Engineering","US22 report filters the same self attendance record by assigned program department");
        foreach(var role in new[]{"admin","mentor","hr"})
        {
            await Login("permission-"+role);
            using var denied=await http.PostAsync("/api/interns/me/attendance/check-in",null);check(denied.StatusCode==HttpStatusCode.Forbidden,role+" cannot use own intern check-in");
            using var contract=await http.GetAsync("/api/interns/me/contract");check(contract.StatusCode==HttpStatusCode.Forbidden,role+" cannot use own intern contract");
            if(role!="hr"){using var deniedHr=await http.GetAsync("/api/contracts");check(deniedHr.StatusCode==HttpStatusCode.Forbidden,role+" cannot manage contracts");}
        }
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"ConnectionStrings:InternManagement",connectionString}}).Build();
        var clock=new TestClock(new DateTimeOffset(2030,1,1,17,5,0,TimeSpan.Zero));var attendance=new OwnAttendanceService(config,clock);
        check(await attendance.RecordAsync(owner,false,CancellationToken.None) is null,"check-in at Vietnam midnight uses next local day");
        clock.Now=clock.Now.AddHours(8);check(await attendance.RecordAsync(owner,true,CancellationToken.None) is null,"clock-controlled eight-hour check-out");
        var timed=await attendance.TodayAsync(owner,CancellationToken.None);check(timed.Date=="2030-01-02"&&timed.CheckIn=="00:05:00"&&timed.Hours==8,"Vietnam timezone and working duration persist correctly");
        await Login("permission-hr");
    }
    private sealed class TestClock(DateTimeOffset now):TimeProvider
    {
        public DateTimeOffset Now=now;
        public override DateTimeOffset GetUtcNow()=>Now;
    }
}
