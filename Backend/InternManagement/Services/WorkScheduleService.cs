using System.Data;
using InternManagement.DTOs;
using Microsoft.Data.SqlClient;
namespace InternManagement.Services;
public sealed class WorkScheduleService(IConfiguration configuration)
{
    private readonly string cs=configuration.GetConnectionString("InternManagement")!;
    public async Task<List<WorkScheduleResponse>> ListAsync(int userId,bool own,int? id,CancellationToken ct)
    {
        await using var c=new SqlConnection(cs);await c.OpenAsync(ct);
        await using var q=new SqlCommand("""
            SELECT s.Id,s.InternId,i.FullName,s.WorkDate,s.StartTime,s.EndTime,s.Note
            FROM dbo.WorkSchedules s JOIN dbo.Interns i ON i.Id=s.InternId
            WHERE (@id IS NULL OR s.Id=@id) AND (@own=0 OR EXISTS(SELECT 1 FROM dbo.Users u WHERE u.Id=@user AND u.Role='INTERN' AND u.Email=i.Email))
            ORDER BY s.WorkDate DESC,s.Id DESC
            """,c);
        q.Parameters.AddWithValue("@id",(object?)id??DBNull.Value);q.Parameters.AddWithValue("@own",own);q.Parameters.AddWithValue("@user",userId);
        var result=new List<WorkScheduleResponse>();await using var r=await q.ExecuteReaderAsync(ct);
        while(await r.ReadAsync(ct))result.Add(new(r.GetInt32(0),r.GetInt32(1),r.GetString(2),DateOnly.FromDateTime(r.GetDateTime(3)),TimeOnly.FromTimeSpan(r.GetTimeSpan(4)),TimeOnly.FromTimeSpan(r.GetTimeSpan(5)),r.IsDBNull(6)?null:r.GetString(6)));
        return result;
    }
    public async Task<List<ScheduleInternOption>> InternsAsync(CancellationToken ct)
    {
        await using var c=new SqlConnection(cs);await c.OpenAsync(ct);await using var q=new SqlCommand("SELECT Id,FullName FROM dbo.Interns ORDER BY FullName,Id",c);
        var result=new List<ScheduleInternOption>();await using var r=await q.ExecuteReaderAsync(ct);
        while(await r.ReadAsync(ct))result.Add(new(r.GetInt32(0),r.GetString(1)));return result;
    }
    public async Task<int?> SaveAsync(int actor,int? id,WorkScheduleRequest request,CancellationToken ct)
    {
        if(request.InternId<=0 || request.WorkDate==default || request.StartTime>=request.EndTime || request.Note?.Length>1000)
            throw new ArgumentException("Ngày/giờ không hợp lệ; giờ bắt đầu phải trước giờ kết thúc.");
        await using var c=new SqlConnection(cs);await c.OpenAsync(ct);
        await using var tx=(SqlTransaction)await c.BeginTransactionAsync(ct);
        await using var q=new SqlCommand("SELECT Id FROM dbo.Interns WITH(UPDLOCK,HOLDLOCK) WHERE Id=@intern",c,tx);
        q.Parameters.AddWithValue("@intern",request.InternId);
        if(await q.ExecuteScalarAsync(ct) is null)throw new ArgumentException("Intern không tồn tại.");
        q.Parameters.AddWithValue("@date",request.WorkDate.ToDateTime(TimeOnly.MinValue));
        // Keep attendance decisions stable once check-in has started.
        q.CommandText="SELECT COUNT(*) FROM dbo.AttendanceRecords WHERE InternId=@intern AND Date=@date";
        if(Convert.ToInt32(await q.ExecuteScalarAsync(ct))>0)throw new InvalidOperationException("Ngày này đã có chấm công; không thể thay đổi lịch.");
        q.CommandText=id is null?"""
            INSERT dbo.WorkSchedules(InternId,WorkDate,StartTime,EndTime,Note,CreatedBy)
            OUTPUT INSERTED.Id VALUES(@intern,@date,@start,@end,@note,@actor)
            """:"""
            UPDATE dbo.WorkSchedules SET StartTime=@start,EndTime=@end,Note=@note,UpdatedAt=SYSDATETIMEOFFSET()
            OUTPUT INSERTED.Id WHERE Id=@id AND InternId=@intern AND WorkDate=@date
            """;
        q.Parameters.AddWithValue("@id",(object?)id??DBNull.Value);q.Parameters.AddWithValue("@start",request.StartTime.ToTimeSpan());q.Parameters.AddWithValue("@end",request.EndTime.ToTimeSpan());
        q.Parameters.AddWithValue("@note",(object?)request.Note?.Trim()??DBNull.Value);q.Parameters.AddWithValue("@actor",actor);
        try {var result=await q.ExecuteScalarAsync(ct);await tx.CommitAsync(ct);return result is null?null:Convert.ToInt32(result);}
        catch(SqlException e)when(e.Number is 2601 or 2627){throw new InvalidOperationException("Intern đã có lịch trong ngày này. Hãy sửa lịch hiện có.");}
    }
}