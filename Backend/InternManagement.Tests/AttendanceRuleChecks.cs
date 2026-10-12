using InternManagement.Services;
using InternManagement.DTOs;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

internal static class AttendanceRuleChecks
{
    public static async Task RunAsync(string connectionString,Action<bool,string> check)
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"ConnectionStrings:InternManagement",connectionString}}).Build();
        await using var sql=new SqlConnection(connectionString);await sql.OpenAsync();
        await using var command=sql.CreateCommand();
        command.CommandText="""
            INSERT dbo.Users(Username,FullName,Email,PasswordHash,Role) VALUES('attendance-rule-mentor','Mentor','attendance-rule-mentor@example.invalid','unused','MENTOR');
            DECLARE @mentor int=SCOPE_IDENTITY();
            INSERT dbo.InternshipPrograms(Name,Department,StartDate,EndDate) VALUES('Attendance rules','IT','2026-01-01','2026-12-31');
            DECLARE @program int=SCOPE_IDENTITY();
            INSERT dbo.Interns(FullName,Email,Phone,School,Major,Status,MentorUserId,InternshipProgramId)
                VALUES('Attendance A','attendance-rule-a@example.invalid','0912345678','ICTU','IT',N'Đang thực tập',@mentor,@program);
            DECLARE @intern int=SCOPE_IDENTITY();
            INSERT dbo.InternDocuments(InternId,Kind,FileName,Content,ReviewStatus) VALUES(@intern,'application','application.pdf',0x255044462D,'approved');
            INSERT dbo.InternContracts(InternId,FileName,Content) VALUES(@intern,'contract.pdf',0x255044462D);
            SELECT @intern;
            """;
        var owner=Convert.ToInt32(await command.ExecuteScalarAsync());
        var clock=new Clock();var service=new OwnAttendanceService(config,clock);
        var scenarios=new[]{("07:45","17:29","ON_TIME","EARLY"),("08:00","17:30","ON_TIME","COMPLETED"),("08:01","18:00","LATE","COMPLETED"),("07:45","18:00","ON_TIME","COMPLETED"),("08:30","17:00","LATE","EARLY")};
        int day=1;
        foreach(var (start,end,inStatus,outStatus) in scenarios)
        {
            clock.Set(day,start);
            check(await service.RecordAsync(owner,true,default) is not null,"checkout before check-in rejected");
            check(await service.RecordAsync(owner,false,default) is null,"server check-in at "+start);
            var today=await service.TodayAsync(owner,default);check(today.CheckInStatus==inStatus,"check-in classification "+start+" "+inStatus);
            check(await service.RecordAsync(owner,false,default) is not null,"duplicate check-in rejected");
            check(await service.RecordAsync(owner,true,default) is not null,"equal timestamp checkout rejected");
            clock.Set(day,end);
            check(await service.RecordAsync(owner,true,default) is null,"server checkout at "+end);
            today=await service.TodayAsync(owner,default);check(today.CheckInStatus==inStatus&&today.CheckOutStatus==outStatus,"independent arrival/departure status "+start+" / "+end);
            check(today.Hours==AttendanceRules.Hours(start,end)&&today.Hours<=9.5m,"duration capped to standard working window");
            if(start=="07:45"&&end=="18:00")check(today.Hours==9.5m,"early arrival and late departure earn no overtime");
            check(await service.RecordAsync(owner,true,default) is not null,"duplicate checkout rejected");
            var report=await new AttendanceReportService(config).GetReportAsync(new AttendanceReportRequest{InternId=owner,StartDate=$"2026-10-{day:00}",EndDate=$"2026-10-{day:00}"});
            check(report.Records.Count==1&&report.Records[0].CheckInStatus==inStatus&&report.Records[0].CheckOutStatus==outStatus&&report.Records[0].Hours==today.Hours,"HR report matches self attendance");
            day++;
        }
        check((await service.HistoryAsync(owner,default)).Count==5,"history retains each workday");
        check((await service.HistoryAsync(int.MaxValue,default)).Count==0,"another owner cannot read attendance rows");
        command.CommandText="DELETE dbo.Interns WHERE Id=@id;DELETE dbo.InternshipPrograms WHERE Name='Attendance rules';DELETE dbo.Users WHERE Username='attendance-rule-mentor'";command.Parameters.AddWithValue("@id",owner);await command.ExecuteNonQueryAsync();
    }
    private sealed class Clock:TimeProvider
    {
        private DateTimeOffset now;
        public void Set(int day,string time)=>now=new DateTimeOffset(2026,10,day,int.Parse(time[..2]),int.Parse(time[3..]),0,TimeSpan.FromHours(7)).ToUniversalTime();
        public override DateTimeOffset GetUtcNow()=>now;
    }
}
