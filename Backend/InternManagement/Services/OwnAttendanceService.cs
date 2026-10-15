using System.Data;
using Microsoft.Data.SqlClient;
namespace InternManagement.Services;
public sealed record OwnAttendanceResponse(string Date,string? CheckIn,string? CheckOut,decimal Hours,string? Status)
{
    public TimeOnly ScheduledStart { get; init; } = AttendanceRules.Start;
    public TimeOnly ScheduledEnd { get; init; } = AttendanceRules.End;
    public string? CheckInStatus => AttendanceRules.CheckInStatus(CheckIn,ScheduledStart);
    public string? CheckOutStatus => AttendanceRules.CheckOutStatus(CheckOut,ScheduledEnd);
}

public sealed class OwnAttendanceService(IConfiguration config,TimeProvider clock)
{
    private readonly string connectionString=config.GetConnectionString("InternManagement")!;
    public async Task<List<OwnAttendanceResponse>> HistoryAsync(int owner,CancellationToken ct)
    {
        await using var connection=new SqlConnection(connectionString);await connection.OpenAsync(ct);
        await using var command=new SqlCommand("SELECT CONVERT(varchar(10),Date,23),CheckIn,CheckOut,WorkingHours,Status,ScheduledStart,ScheduledEnd FROM dbo.AttendanceRecords WHERE InternId=@Id ORDER BY Date DESC,Id DESC",connection);
        command.Parameters.Add("@Id",SqlDbType.Int).Value=owner;
        await using var reader=await command.ExecuteReaderAsync(ct);var rows=new List<OwnAttendanceResponse>();
        while(await reader.ReadAsync(ct))rows.Add(new(reader.GetString(0),reader.IsDBNull(1)?null:reader.GetString(1),reader.IsDBNull(2)?null:reader.GetString(2),AttendanceRules.Hours(reader.IsDBNull(1)?null:reader.GetString(1),reader.IsDBNull(2)?null:reader.GetString(2),ReadTime(reader,5,AttendanceRules.Start),ReadTime(reader,6,AttendanceRules.End)),reader.GetString(4)){ScheduledStart=ReadTime(reader,5,AttendanceRules.Start),ScheduledEnd=ReadTime(reader,6,AttendanceRules.End)});
        return rows;
    }
    public async Task<OwnAttendanceResponse> TodayAsync(int owner,CancellationToken ct)
    {
        var today=clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).Date;
        await using var connection=new SqlConnection(connectionString);await connection.OpenAsync(ct);
        await using var cmd=connection.CreateCommand();cmd.CommandText="SELECT TOP(1) a.CheckIn,a.CheckOut,a.WorkingHours,a.Status,COALESCE(a.ScheduledStart,s.StartTime),COALESCE(a.ScheduledEnd,s.EndTime) FROM (SELECT @Id AS InternId,@Date AS WorkDate) d LEFT JOIN dbo.AttendanceRecords a ON a.InternId=d.InternId AND a.Date=d.WorkDate LEFT JOIN dbo.WorkSchedules s ON s.InternId=d.InternId AND s.WorkDate=d.WorkDate ORDER BY a.Id";
        cmd.Parameters.Add("@Id",SqlDbType.Int).Value=owner;cmd.Parameters.Add("@Date",SqlDbType.Date).Value=today;
        await using var reader=await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)?new(today.ToString("yyyy-MM-dd"),reader.IsDBNull(0)?null:reader.GetString(0),reader.IsDBNull(1)?null:reader.GetString(1),AttendanceRules.Hours(reader.IsDBNull(0)?null:reader.GetString(0),reader.IsDBNull(1)?null:reader.GetString(1),ReadTime(reader,4,AttendanceRules.Start),ReadTime(reader,5,AttendanceRules.End)),reader.IsDBNull(3)?null:reader.GetString(3)){ScheduledStart=ReadTime(reader,4,AttendanceRules.Start),ScheduledEnd=ReadTime(reader,5,AttendanceRules.End)}:new(today.ToString("yyyy-MM-dd"),null,null,0,null);
    }
    private static TimeOnly ReadTime(SqlDataReader r,int column,TimeOnly fallback)=>r.IsDBNull(column)?fallback:TimeOnly.FromTimeSpan(r.GetTimeSpan(column));
    public async Task<string?> RecordAsync(int owner,bool checkOut,CancellationToken ct)
    {
        var now=clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7));
        await using var connection=new SqlConnection(connectionString);await connection.OpenAsync(ct);
        await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(ct);
        await using var cmd=connection.CreateCommand();cmd.Transaction=transaction;
        cmd.CommandText="SELECT Id FROM dbo.Interns WITH(UPDLOCK,HOLDLOCK) WHERE Id=@Owner";
        cmd.Parameters.Add("@Owner",SqlDbType.Int).Value=owner;
        if(await cmd.ExecuteScalarAsync(ct) is null)return "Chưa có hồ sơ thực tập.";
        if(!checkOut)
        {
            cmd.Parameters.Add("@WorkDate",SqlDbType.Date).Value=now.Date;
            cmd.CommandText="""
                SELECT COUNT(*) FROM dbo.Interns i JOIN dbo.InternshipPrograms p ON p.Id=i.InternshipProgramId
                WHERE i.Id=@Owner AND i.MentorUserId IS NOT NULL AND i.Status=N'Đang thực tập'
                AND @WorkDate BETWEEN p.StartDate AND p.EndDate
                AND EXISTS(SELECT 1 FROM dbo.InternContracts c WHERE c.InternId=i.Id)
                AND EXISTS(SELECT 1 FROM dbo.InternDocuments d WHERE d.InternId=i.Id AND d.Kind='application' AND d.ReviewStatus='approved')
                AND NOT EXISTS(SELECT 1 FROM dbo.InternDocuments d WHERE d.InternId=i.Id AND d.ReviewStatus='rejected')
                """;
            if(Convert.ToInt32(await cmd.ExecuteScalarAsync(ct))==0)return "Chưa đủ điều kiện thực tập: cần hồ sơ đã duyệt, hợp đồng, chương trình và Mentor.";
        }
        cmd.CommandText="SELECT Id,CheckIn,CheckOut,ScheduledStart,ScheduledEnd FROM dbo.AttendanceRecords WITH(UPDLOCK,HOLDLOCK) WHERE InternId=@Owner AND Date=@Date";
        cmd.Parameters.Add("@Date",SqlDbType.Date).Value=now.Date;
        var scheduledStart=AttendanceRules.Start;var scheduledEnd=AttendanceRules.End;
        int? id=null;string? start=null,end=null;
        await using(var reader=await cmd.ExecuteReaderAsync(ct))
        {
            if(await reader.ReadAsync(ct)){id=reader.GetInt32(0);start=reader.IsDBNull(1)?null:reader.GetString(1);end=reader.IsDBNull(2)?null:reader.GetString(2);scheduledStart=ReadTime(reader,3,AttendanceRules.Start);scheduledEnd=ReadTime(reader,4,AttendanceRules.End);}
            if(await reader.ReadAsync(ct))return "Có nhiều bản ghi trong ngày. Vui lòng liên hệ HR.";
        }
        if(!checkOut&&id is not null)return "Ngày hôm nay đã có bản ghi chấm công.";
        if(checkOut&&(id is null||string.IsNullOrEmpty(start)))return "Bạn chưa check-in hôm nay.";
        if(checkOut&&!string.IsNullOrEmpty(end))return "Bạn đã check-out hôm nay.";
        if(!checkOut) {
            cmd.CommandText="SELECT StartTime,EndTime FROM dbo.WorkSchedules WHERE InternId=@Owner AND WorkDate=@Date";
            await using var schedule=await cmd.ExecuteReaderAsync(ct);
            if(await schedule.ReadAsync(ct)){scheduledStart=TimeOnly.FromTimeSpan(schedule.GetTimeSpan(0));scheduledEnd=TimeOnly.FromTimeSpan(schedule.GetTimeSpan(1));}
        }
        cmd.Parameters.AddWithValue("@ScheduledStart",scheduledStart.ToTimeSpan());cmd.Parameters.AddWithValue("@ScheduledEnd",scheduledEnd.ToTimeSpan());
        var time=now.ToString("HH:mm:ss");
        cmd.Parameters.Add("@Time",SqlDbType.NVarChar,10).Value=time;
        cmd.Parameters.Add("@Status",SqlDbType.VarChar,30).Value=AttendanceRules.StoredStatus(checkOut?start:time,checkOut?time:null,scheduledStart,scheduledEnd);
        if(!checkOut)cmd.CommandText="INSERT dbo.AttendanceRecords(InternId,Date,CheckIn,Status,ScheduledStart,ScheduledEnd) VALUES(@Owner,@Date,@Time,@Status,@ScheduledStart,@ScheduledEnd)";
        else
        {
            if(!TimeOnly.TryParse(start,out var checkInTime))return "Giờ check-in không hợp lệ. Vui lòng liên hệ HR.";
            var hours=AttendanceRules.Hours(start,time,scheduledStart,scheduledEnd);
            if(TimeOnly.Parse(time)<=checkInTime)return "Giờ check-out phải sau giờ check-in.";
            cmd.Parameters.Add("@Id",SqlDbType.Int).Value=id!.Value;
            var hoursParameter=cmd.Parameters.Add("@Hours",SqlDbType.Decimal);hoursParameter.Precision=4;hoursParameter.Scale=1;hoursParameter.Value=Math.Round(hours,1);
            cmd.CommandText="UPDATE dbo.AttendanceRecords SET CheckOut=@Time,WorkingHours=@Hours,Status=@Status WHERE Id=@Id";
        }
        await cmd.ExecuteNonQueryAsync(ct);await transaction.CommitAsync(ct);return null;
    }
}
