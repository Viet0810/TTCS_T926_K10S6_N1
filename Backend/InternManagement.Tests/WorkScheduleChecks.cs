using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InternManagement.DTOs;
using InternManagement.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
internal static class WorkScheduleChecks {
 public static async Task RunAsync(HttpClient http,string cs,Action<bool,string> check) {
  await using var sql=new SqlConnection(cs);await sql.OpenAsync();await using var q=sql.CreateCommand();
  q.CommandText="SELECT Id FROM dbo.Interns WHERE Email='report-ia@example.org'";var intern=Convert.ToInt32(await q.ExecuteScalarAsync());
  async Task Login(string username){http.DefaultRequestHeaders.Authorization=null;var r=await http.PostAsJsonAsync("/api/auth/login",new{username,password="Progress-report-test-9"});using var j=JsonDocument.Parse(await r.Content.ReadAsStringAsync());http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",j.RootElement.GetProperty("token").GetString());}
  var request=new WorkScheduleRequest(intern,new DateOnly(2026,10,20),new TimeOnly(9,0),new TimeOnly(18,0),"Custom hours");
  await Login("report-hr");
  var created=await http.PostAsJsonAsync("/api/work-schedules",request);check(created.IsSuccessStatusCode,"US23 HR creates 09:00-18:00");using var doc=JsonDocument.Parse(await created.Content.ReadAsStringAsync());var id=doc.RootElement.GetProperty("id").GetInt32();
  check((await http.PostAsJsonAsync("/api/work-schedules",request)).StatusCode==HttpStatusCode.Conflict,"US23 duplicate day rejected");
  check((await http.PostAsJsonAsync("/api/work-schedules",request with{EndTime=new TimeOnly(8,0)})).StatusCode==HttpStatusCode.BadRequest,"US23 invalid time rejected");
  check((await http.PutAsJsonAsync("/api/work-schedules/"+id,request with{Note="Updated"})).IsSuccessStatusCode,"US23 HR updates schedule");
  q.CommandText="DELETE dbo.RolePermissions WHERE Role='HR' AND Permission='WORK_SCHEDULE_CREATE'";await q.ExecuteNonQueryAsync();
  check((await http.PostAsJsonAsync("/api/work-schedules",request)).StatusCode==HttpStatusCode.Forbidden,"US23 revoke returns 403");
  q.CommandText="INSERT dbo.RolePermissions(Role,Permission) VALUES('HR','WORK_SCHEDULE_CREATE')";await q.ExecuteNonQueryAsync();
  await Login("report-ia");check((await http.GetAsync("/api/work-schedules/"+id)).IsSuccessStatusCode,"US23 Intern reads own schedule");
  check((await http.PostAsJsonAsync("/api/work-schedules",request)).StatusCode==HttpStatusCode.Forbidden,"US23 Intern cannot write");
  await Login("report-ib");check((await http.GetAsync("/api/work-schedules/"+id)).StatusCode==HttpStatusCode.Forbidden,"US23 foreign schedule 403");
  var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"ConnectionStrings:InternManagement",cs}}).Build();
  q.CommandText=$"""
   UPDATE dbo.Interns SET Status=N'Đang thực tập',InternshipProgramId=(SELECT TOP(1) Id FROM dbo.InternshipPrograms) WHERE Id={intern};
   INSERT dbo.InternDocuments(InternId,Kind,FileName,Content,ReviewStatus) VALUES({intern},'application','test.pdf',0x255044462D,'approved');
   INSERT dbo.InternContracts(InternId,FileName,Content) VALUES({intern},'contract.pdf',0x255044462D);
   """;
  await q.ExecuteNonQueryAsync();
  var clock=new Clock();var attendance=new OwnAttendanceService(config,clock);var schedules=new WorkScheduleService(config);
  for(var n=0;n<2;n++) {
   var day=20+n; if(n==1)await schedules.SaveAsync(1,null,request with{WorkDate=new DateOnly(2026,10,day)},default);
   clock.Set(day,n==0?8:9,n==0?55:5);check(await attendance.RecordAsync(intern,false,default)is null,"US23 scheduled checkin");
   var row=await attendance.TodayAsync(intern,default);check(row.CheckInStatus==(n==0?"ON_TIME":"LATE"),"US23 08:55 OnTime / 09:05 Late");
   clock.Set(day,n==0?17:18,n==0?30:0);check(await attendance.RecordAsync(intern,true,default)is null,"US23 scheduled checkout");
   row=await attendance.TodayAsync(intern,default);check(row.CheckOutStatus==(n==0?"EARLY":"COMPLETED")&&row.Hours<=9,"US23 17:30 Early / 18:00 Completed; no overtime");
   var report=await new AttendanceReportService(config).GetReportAsync(new AttendanceReportRequest{InternId=intern,StartDate=$"2026-10-{day}",EndDate=$"2026-10-{day}"});
   check(report.Records.Single().CheckOutStatus==row.CheckOutStatus&&report.Records.Single().Hours==row.Hours,"US23 HR attendance agrees with schedule");
  }
  clock.Set(22,8,0);check(await attendance.RecordAsync(intern,false,default)is null,"US23 no custom schedule uses default checkin");
  var fallback=await attendance.TodayAsync(intern,default);
  check(fallback.ScheduledStart==new TimeOnly(8,0)&&fallback.ScheduledEnd==new TimeOnly(17,30)&&fallback.CheckInStatus=="ON_TIME","US23 fallback remains 08:00-17:30");
  clock.Set(22,17,30);check(await attendance.RecordAsync(intern,true,default)is null,"US23 default checkout succeeds");
  fallback=await attendance.TodayAsync(intern,default);
  check(fallback.CheckOutStatus=="COMPLETED"&&fallback.Hours==9.5m,"US23 fallback attendance calculation uses 08:00-17:30");
  try{await schedules.SaveAsync(1,id,request,default);check(false,"US23 checked-in schedule cannot change");}catch(InvalidOperationException){check(true,"US23 checked-in schedule protected");}
 }
 private sealed class Clock:TimeProvider {private DateTimeOffset now;public void Set(int day,int hour,int minute)=>now=new DateTimeOffset(2026,10,day,hour,minute,0,TimeSpan.FromHours(7));public override DateTimeOffset GetUtcNow()=>now.ToUniversalTime();}
}
