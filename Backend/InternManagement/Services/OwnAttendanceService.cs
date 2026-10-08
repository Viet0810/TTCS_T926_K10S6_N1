using System.Data;
using Microsoft.Data.SqlClient;
namespace InternManagement.Services;
public sealed record OwnAttendanceResponse(string Date,string? CheckIn,string? CheckOut,decimal Hours,string? Status);

public sealed class OwnAttendanceService(IConfiguration config,TimeProvider clock)
{
    private readonly string connectionString=config.GetConnectionString("InternManagement")!;
    public async Task<OwnAttendanceResponse> TodayAsync(int owner,CancellationToken ct)
    {
        var today=clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).Date;
        await using var connection=new SqlConnection(connectionString);await connection.OpenAsync(ct);
        await using var cmd=connection.CreateCommand();cmd.CommandText="SELECT TOP(1) CheckIn,CheckOut,WorkingHours,Status FROM dbo.AttendanceRecords WHERE InternId=@Id AND Date=@Date ORDER BY Id";
        cmd.Parameters.Add("@Id",SqlDbType.Int).Value=owner;cmd.Parameters.Add("@Date",SqlDbType.Date).Value=today;
        await using var reader=await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)?new(today.ToString("yyyy-MM-dd"),reader.IsDBNull(0)?null:reader.GetString(0),reader.IsDBNull(1)?null:reader.GetString(1),reader.GetDecimal(2),reader.GetString(3)):new(today.ToString("yyyy-MM-dd"),null,null,0,null);
    }
    public async Task<string?> RecordAsync(int owner,bool checkOut,CancellationToken ct)
    {
        var now=clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7));
        await using var connection=new SqlConnection(connectionString);await connection.OpenAsync(ct);
        await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(ct);
        await using var cmd=connection.CreateCommand();cmd.Transaction=transaction;
        cmd.CommandText="SELECT Id FROM dbo.Interns WITH(UPDLOCK,HOLDLOCK) WHERE Id=@Owner";
        cmd.Parameters.Add("@Owner",SqlDbType.Int).Value=owner;
        if(await cmd.ExecuteScalarAsync(ct) is null)return "Chưa có hồ sơ thực tập.";
        cmd.CommandText="SELECT Id,CheckIn,CheckOut FROM dbo.AttendanceRecords WITH(UPDLOCK,HOLDLOCK) WHERE InternId=@Owner AND Date=@Date";
        cmd.Parameters.Add("@Date",SqlDbType.Date).Value=now.Date;
        int? id=null;string? start=null,end=null;
        await using(var reader=await cmd.ExecuteReaderAsync(ct))
        {
            if(await reader.ReadAsync(ct)){id=reader.GetInt32(0);start=reader.IsDBNull(1)?null:reader.GetString(1);end=reader.IsDBNull(2)?null:reader.GetString(2);}
            if(await reader.ReadAsync(ct))return "Có nhiều bản ghi trong ngày. Vui lòng liên hệ HR.";
        }
        if(!checkOut&&id is not null)return "Ngày hôm nay đã có bản ghi chấm công.";
        if(checkOut&&(id is null||string.IsNullOrEmpty(start)))return "Bạn chưa check-in hôm nay.";
        if(checkOut&&!string.IsNullOrEmpty(end))return "Bạn đã check-out hôm nay.";
        cmd.Parameters.Add("@Time",SqlDbType.NVarChar,10).Value=now.ToString("HH:mm:ss");
        if(!checkOut)cmd.CommandText="INSERT dbo.AttendanceRecords(InternId,Date,CheckIn,Status) VALUES(@Owner,@Date,@Time,'ON_TIME')";
        else
        {
            if(!TimeOnly.TryParse(start,out var checkInTime))return "Giờ check-in không hợp lệ. Vui lòng liên hệ HR.";
            var hours=(decimal)(now.TimeOfDay-checkInTime.ToTimeSpan()).TotalHours;
            if(hours<0)return "Giờ check-out không được trước giờ check-in.";
            cmd.Parameters.Add("@Id",SqlDbType.Int).Value=id!.Value;
            var hoursParameter=cmd.Parameters.Add("@Hours",SqlDbType.Decimal);hoursParameter.Precision=4;hoursParameter.Scale=1;hoursParameter.Value=Math.Round(hours,1);
            cmd.CommandText="UPDATE dbo.AttendanceRecords SET CheckOut=@Time,WorkingHours=@Hours WHERE Id=@Id";
        }
        await cmd.ExecuteNonQueryAsync(ct);await transaction.CommitAsync(ct);return null;
    }
}
