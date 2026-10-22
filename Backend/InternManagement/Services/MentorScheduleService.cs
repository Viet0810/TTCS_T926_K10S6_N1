using System.Data;
using InternManagement.DTOs;
using Microsoft.Data.SqlClient;

namespace InternManagement.Services;

public sealed class MentorScheduleService(IConfiguration configuration)
{
    private readonly string cs=configuration.GetConnectionString("InternManagement")!;
    private static readonly HashSet<string> Types=["WORK","MEETING","TRAINING","OTHER"];
    private static readonly HashSet<string> Recurrences=["NONE","WEEKLY","CUSTOM"];
    private static readonly HashSet<string> Statuses=["ACTIVE","PAUSED","CANCELLED"];

    public async Task<List<ScheduleMentorOption>> MentorsAsync(CancellationToken ct)
    {
        await using var c=new SqlConnection(cs);await c.OpenAsync(ct);
        await using var q=new SqlCommand("SELECT u.Id,u.FullName,COUNT(i.Id) FROM dbo.Users u LEFT JOIN dbo.Interns i ON i.MentorUserId=u.Id WHERE u.Role='MENTOR' GROUP BY u.Id,u.FullName ORDER BY u.FullName",c);
        var result=new List<ScheduleMentorOption>();await using var r=await q.ExecuteReaderAsync(ct);
        while(await r.ReadAsync(ct))result.Add(new(r.GetInt32(0),r.GetString(1),r.GetInt32(2)));return result;
    }

    public async Task<List<SchedulePerson>> InternsAsync(int mentorId,CancellationToken ct)
    {
        await using var c=new SqlConnection(cs);await c.OpenAsync(ct);
        await using var q=new SqlCommand("SELECT Id,FullName,Email FROM dbo.Interns WHERE MentorUserId=@mentor ORDER BY FullName",c);q.Parameters.AddWithValue("@mentor",mentorId);
        var result=new List<SchedulePerson>();await using var r=await q.ExecuteReaderAsync(ct);
        while(await r.ReadAsync(ct))result.Add(new(r.GetInt32(0),r.GetString(1),r.GetString(2)));return result;
    }

    public async Task<List<MentorWorkScheduleResponse>> ListAsync(int userId,string role,DateOnly from,DateOnly to,int? mentorId,int? internId,string? type,string? status,CancellationToken ct)
    {
        if(to<from||to.DayNumber-from.DayNumber>370)throw new ArgumentException("Khoảng ngày không hợp lệ hoặc vượt quá 370 ngày.");
        await using var c=new SqlConnection(cs);await c.OpenAsync(ct);
        await using var q=new SqlCommand("""
        SELECT s.Id,s.MentorUserId,u.FullName,s.Title,s.ScheduleType,s.StartDate,s.EndDate,s.StartTime,s.EndTime,s.RecurrenceType,s.WeekDays,s.Note,s.Status
        FROM dbo.MentorScheduleSeries s JOIN dbo.Users u ON u.Id=s.MentorUserId
        WHERE s.StartDate<=@to AND s.EndDate>=@from
          AND (@mentor IS NULL OR s.MentorUserId=@mentor) AND (@type IS NULL OR s.ScheduleType=@type) AND (@status IS NULL OR s.Status=@status)
          AND (@role IN('HR','ADMIN') OR (@role='MENTOR' AND s.MentorUserId=@user)
            OR (@role='INTERN' AND EXISTS(SELECT 1 FROM dbo.Users me JOIN dbo.Interns i ON i.Email=me.Email WHERE me.Id=@user AND i.MentorUserId=s.MentorUserId)))
          AND (@intern IS NULL OR EXISTS(SELECT 1 FROM dbo.Interns i WHERE i.Id=@intern AND i.MentorUserId=s.MentorUserId));
        """,c);
        q.Parameters.AddWithValue("@from",from.ToDateTime(TimeOnly.MinValue));q.Parameters.AddWithValue("@to",to.ToDateTime(TimeOnly.MinValue));q.Parameters.AddWithValue("@user",userId);q.Parameters.AddWithValue("@role",role);
        q.Parameters.AddWithValue("@mentor",(object?)mentorId??DBNull.Value);q.Parameters.AddWithValue("@intern",(object?)internId??DBNull.Value);q.Parameters.AddWithValue("@type",(object?)type??DBNull.Value);q.Parameters.AddWithValue("@status",(object?)status??DBNull.Value);
        var series=new List<Series>();await using(var r=await q.ExecuteReaderAsync(ct)){while(await r.ReadAsync(ct))series.Add(new(r.GetInt32(0),r.GetInt32(1),r.GetString(2),r.GetString(3),r.GetString(4),DateOnly.FromDateTime(r.GetDateTime(5)),DateOnly.FromDateTime(r.GetDateTime(6)),TimeOnly.FromTimeSpan(r.GetTimeSpan(7)),TimeOnly.FromTimeSpan(r.GetTimeSpan(8)),r.GetString(9),r.IsDBNull(10)?[]:r.GetString(10).Split(',').Select(int.Parse).ToArray(),r.IsDBNull(11)?null:r.GetString(11),r.GetString(12)));}
        var output=new List<MentorWorkScheduleResponse>();
        foreach(var s in series){var interns=await InternsAsync(s.MentorId,ct);var exceptions=await ExceptionsAsync(s.Id,ct);foreach(var date in Occurrences(s,from,to)){var ex=exceptions.GetValueOrDefault(date);if(ex?.Cancelled==true)continue;output.Add(new(s.Id,s.Id,s.MentorId,s.MentorName,ex?.Title??s.Title,s.Type,date,s.StartDate,s.EndDate,ex?.Start??s.StartTime,ex?.End??s.EndTime,s.Recurrence,s.Days,ex?.Note??s.Note,s.Status,interns));}}
        return output.OrderBy(x=>x.Date).ThenBy(x=>x.StartTime).ToList();
    }

    public async Task<int> CreateAsync(int actor,MentorWorkScheduleRequest request,CancellationToken ct)=>await SaveAllAsync(actor,null,request,ct);
    public async Task<int> UpdateAsync(int actor,int id,ScheduleChangeRequest change,CancellationToken ct)
    {
        var scope=(change.Scope??"").ToUpperInvariant();if(change.Schedule is null)throw new ArgumentException("Thiếu dữ liệu lịch cập nhật.");
        if(scope=="ALL")return await SaveAllAsync(actor,id,change.Schedule,ct);
        if(change.OccurrenceDate is null)throw new ArgumentException("Vui lòng chọn ngày áp dụng thay đổi.");
        if(scope=="THIS"){await UpsertExceptionAsync(actor,id,change.OccurrenceDate.Value,false,change.Schedule,ct);return id;}
        if(scope!="FUTURE")throw new ArgumentException("Phạm vi cập nhật không hợp lệ.");
        await ShortenAsync(id,change.OccurrenceDate.Value.AddDays(-1),ct);return await SaveAllAsync(actor,null,change.Schedule with{StartDate=change.OccurrenceDate.Value},ct);
    }
    public async Task DeleteAsync(int actor,int id,string scope,DateOnly? date,CancellationToken ct)
    {
        scope=scope.ToUpperInvariant();if(scope=="THIS"&&date.HasValue){await UpsertExceptionAsync(actor,id,date.Value,true,null,ct);return;}
        await using var c=new SqlConnection(cs);await c.OpenAsync(ct);await using var q=c.CreateCommand();
        if(scope=="FUTURE"&&date.HasValue){q.CommandText="UPDATE dbo.MentorScheduleSeries SET EndDate=DATEADD(day,-1,@date),UpdatedAt=SYSDATETIMEOFFSET() WHERE Id=@id AND StartDate<@date";q.Parameters.AddWithValue("@date",date.Value.ToDateTime(TimeOnly.MinValue));}
        else if(scope=="ALL")q.CommandText="UPDATE dbo.MentorScheduleSeries SET Status='CANCELLED',UpdatedAt=SYSDATETIMEOFFSET() WHERE Id=@id";else throw new ArgumentException("Phạm vi hủy không hợp lệ.");
        q.Parameters.AddWithValue("@id",id);if(await q.ExecuteNonQueryAsync(ct)==0)throw new KeyNotFoundException();
    }

    private async Task<int> SaveAllAsync(int actor,int? id,MentorWorkScheduleRequest x,CancellationToken ct)
    {
        Validate(x);await using var c=new SqlConnection(cs);await c.OpenAsync(ct);await using var tx=(SqlTransaction)await c.BeginTransactionAsync(ct);await using var q=c.CreateCommand();q.Transaction=tx;
        q.CommandText="SELECT COUNT(*) FROM dbo.Users u JOIN dbo.Interns i ON i.MentorUserId=u.Id WHERE u.Id=@mentor AND u.Role='MENTOR'";q.Parameters.AddWithValue("@mentor",x.MentorId);if(Convert.ToInt32(await q.ExecuteScalarAsync(ct))==0)throw new ArgumentException("Mentor chưa có Intern được phân công nên không thể tạo lịch.");
        q.CommandText="SELECT COUNT(*) FROM dbo.MentorScheduleSeries WHERE MentorUserId=@mentor AND Status='ACTIVE' AND Id<>COALESCE(@id,0) AND StartDate<=@endDate AND EndDate>=@startDate AND StartTime<@endTime AND EndTime>@startTime";
        q.Parameters.Add("@id",SqlDbType.Int).Value=(object?)id??DBNull.Value;q.Parameters.AddWithValue("@startDate",x.StartDate.ToDateTime(TimeOnly.MinValue));q.Parameters.AddWithValue("@endDate",x.EndDate.ToDateTime(TimeOnly.MinValue));q.Parameters.AddWithValue("@startTime",x.StartTime.ToTimeSpan());q.Parameters.AddWithValue("@endTime",x.EndTime.ToTimeSpan());
        if(Convert.ToInt32(await q.ExecuteScalarAsync(ct))>0)throw new InvalidOperationException("Lịch bị trùng thời gian với một lịch đang hoạt động của Mentor.");
        q.CommandText=id is null?"INSERT dbo.MentorScheduleSeries(MentorUserId,Title,ScheduleType,StartDate,EndDate,StartTime,EndTime,RecurrenceType,WeekDays,Note,Status,CreatedBy) OUTPUT INSERTED.Id VALUES(@mentor,@title,@type,@startDate,@endDate,@startTime,@endTime,@recurrence,@days,@note,@status,@actor)":"UPDATE dbo.MentorScheduleSeries SET MentorUserId=@mentor,Title=@title,ScheduleType=@type,StartDate=@startDate,EndDate=@endDate,StartTime=@startTime,EndTime=@endTime,RecurrenceType=@recurrence,WeekDays=@days,Note=@note,Status=@status,UpdatedBy=@actor,UpdatedAt=SYSDATETIMEOFFSET() OUTPUT INSERTED.Id WHERE Id=@id";
        q.Parameters.AddWithValue("@title",x.Title.Trim());q.Parameters.AddWithValue("@type",x.ScheduleType.ToUpperInvariant());q.Parameters.AddWithValue("@recurrence",x.RecurrenceType.ToUpperInvariant());q.Parameters.AddWithValue("@days",x.WeekDays.Length==0?DBNull.Value:string.Join(',',x.WeekDays));q.Parameters.AddWithValue("@note",(object?)x.Note?.Trim()??DBNull.Value);q.Parameters.AddWithValue("@status",x.Status.ToUpperInvariant());q.Parameters.AddWithValue("@actor",actor);
        var result=await q.ExecuteScalarAsync(ct)??throw new KeyNotFoundException();await tx.CommitAsync(ct);return Convert.ToInt32(result);
    }
    private static void Validate(MentorWorkScheduleRequest x){if(string.IsNullOrWhiteSpace(x.Title)||x.StartDate==default||x.EndDate<x.StartDate||x.StartTime>=x.EndTime)throw new ArgumentException("Ngày hoặc giờ không hợp lệ; giờ kết thúc phải sau giờ bắt đầu.");if(!Types.Contains(x.ScheduleType.ToUpperInvariant())||!Recurrences.Contains(x.RecurrenceType.ToUpperInvariant())||!Statuses.Contains(x.Status.ToUpperInvariant()))throw new ArgumentException("Loại lịch, lặp lại hoặc trạng thái không hợp lệ.");if(x.RecurrenceType!="NONE"&&(x.WeekDays.Length==0||x.WeekDays.Any(d=>d<1||d>7)))throw new ArgumentException("Lịch lặp phải chọn ít nhất một ngày hợp lệ trong tuần.");}
    private async Task ShortenAsync(int id,DateOnly end,CancellationToken ct){await using var c=new SqlConnection(cs);await c.OpenAsync(ct);await using var q=new SqlCommand("UPDATE dbo.MentorScheduleSeries SET EndDate=@end WHERE Id=@id AND StartDate<=@end",c);q.Parameters.AddWithValue("@id",id);q.Parameters.AddWithValue("@end",end.ToDateTime(TimeOnly.MinValue));if(await q.ExecuteNonQueryAsync(ct)==0)throw new ArgumentException("Ngày áp dụng không thuộc chuỗi lịch.");}
    private async Task UpsertExceptionAsync(int actor,int id,DateOnly date,bool cancel,MentorWorkScheduleRequest? x,CancellationToken ct){await using var c=new SqlConnection(cs);await c.OpenAsync(ct);await using var q=new SqlCommand("MERGE dbo.MentorScheduleExceptions AS t USING(SELECT @id SeriesId,@date OccurrenceDate)s ON t.SeriesId=s.SeriesId AND t.OccurrenceDate=s.OccurrenceDate WHEN MATCHED THEN UPDATE SET IsCancelled=@cancel,OverrideTitle=@title,OverrideStartTime=@start,OverrideEndTime=@end,OverrideNote=@note WHEN NOT MATCHED THEN INSERT(SeriesId,OccurrenceDate,IsCancelled,OverrideTitle,OverrideStartTime,OverrideEndTime,OverrideNote,CreatedBy)VALUES(@id,@date,@cancel,@title,@start,@end,@note,@actor);",c);q.Parameters.AddWithValue("@id",id);q.Parameters.AddWithValue("@date",date.ToDateTime(TimeOnly.MinValue));q.Parameters.AddWithValue("@cancel",cancel);q.Parameters.AddWithValue("@title",(object?)x?.Title??DBNull.Value);q.Parameters.AddWithValue("@start",(object?)x?.StartTime.ToTimeSpan()??DBNull.Value);q.Parameters.AddWithValue("@end",(object?)x?.EndTime.ToTimeSpan()??DBNull.Value);q.Parameters.AddWithValue("@note",(object?)x?.Note??DBNull.Value);q.Parameters.AddWithValue("@actor",actor);await q.ExecuteNonQueryAsync(ct);}
    private async Task<Dictionary<DateOnly,ExceptionRow>> ExceptionsAsync(int id,CancellationToken ct){await using var c=new SqlConnection(cs);await c.OpenAsync(ct);await using var q=new SqlCommand("SELECT OccurrenceDate,IsCancelled,OverrideTitle,OverrideStartTime,OverrideEndTime,OverrideNote FROM dbo.MentorScheduleExceptions WHERE SeriesId=@id",c);q.Parameters.AddWithValue("@id",id);var result=new Dictionary<DateOnly,ExceptionRow>();await using var r=await q.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct)){var d=DateOnly.FromDateTime(r.GetDateTime(0));result[d]=new(r.GetBoolean(1),r.IsDBNull(2)?null:r.GetString(2),r.IsDBNull(3)?null:TimeOnly.FromTimeSpan(r.GetTimeSpan(3)),r.IsDBNull(4)?null:TimeOnly.FromTimeSpan(r.GetTimeSpan(4)),r.IsDBNull(5)?null:r.GetString(5));}return result;}
    private static IEnumerable<DateOnly> Occurrences(Series s,DateOnly from,DateOnly to){var begin=s.StartDate>from?s.StartDate:from;var end=s.EndDate<to?s.EndDate:to;for(var d=begin;d<=end;d=d.AddDays(1)){var weekday=(int)d.DayOfWeek;weekday=weekday==0?7:weekday;if(s.Recurrence=="NONE"&&d==s.StartDate||s.Recurrence!="NONE"&&s.Days.Contains(weekday))yield return d;}}
    private sealed record Series(int Id,int MentorId,string MentorName,string Title,string Type,DateOnly StartDate,DateOnly EndDate,TimeOnly StartTime,TimeOnly EndTime,string Recurrence,int[] Days,string? Note,string Status);
    private sealed record ExceptionRow(bool Cancelled,string? Title,TimeOnly? Start,TimeOnly? End,string? Note);
}
