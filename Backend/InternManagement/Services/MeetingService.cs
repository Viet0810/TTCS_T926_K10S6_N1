using System.Data;
using InternManagement.DTOs;
using Microsoft.Data.SqlClient;

namespace InternManagement.Services;

public sealed class MeetingService(IConfiguration configuration,IEmailService email,TimeProvider clock,ILogger<MeetingService> logger)
{
    private const string CreatedTrigger="MEETING_CREATED";
    private readonly string connectionString=configuration.GetConnectionString("InternManagement")??throw new InvalidOperationException("Chưa cấu hình cơ sở dữ liệu.");

    public async Task<MeetingResponse> CreateAsync(int creatorUserId,CreateMeetingRequest request,CancellationToken ct)
    {
        var title=request.Title?.Trim();var participants=request.ParticipantUserIds?.Distinct().ToArray()??[];
        if(string.IsNullOrWhiteSpace(title)||title.Length>200)throw new ArgumentException("Tiêu đề cuộc họp là bắt buộc và tối đa 200 ký tự.");
        if(request.StartTime<=clock.GetUtcNow())throw new ArgumentException("Thời gian họp phải ở tương lai.");
        if(request.EndTime<=request.StartTime)throw new ArgumentException("Thời gian kết thúc phải sau thời gian bắt đầu.");
        if(request.ClientRequestId==Guid.Empty)throw new ArgumentException("Mã yêu cầu tạo lịch họp không hợp lệ.");
        if(request.LocationOrLink?.Length>500||request.Description?.Length>2000)
            throw new ArgumentException("Địa điểm/link tối đa 500 ký tự; mô tả tối đa 2000 ký tự.");
        if(participants.Length==0||participants.Any(id=>id<=0))throw new ArgumentException("Cuộc họp phải có ít nhất một người tham gia hợp lệ.");
        await using var connection=new SqlConnection(connectionString);await connection.OpenAsync(ct);
        await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await using var command=connection.CreateCommand();command.Transaction=transaction;
        command.CommandText="SELECT Id FROM dbo.Meetings WITH(UPDLOCK,HOLDLOCK) WHERE CreatedByUserId=@creator AND ClientRequestId=@requestId";
        command.Parameters.Add("@creator",SqlDbType.Int).Value=creatorUserId;command.Parameters.Add("@requestId",SqlDbType.UniqueIdentifier).Value=request.ClientRequestId;
        var existing=await command.ExecuteScalarAsync(ct);
        if(existing is not null){await transaction.CommitAsync(ct);return await LoadResponseAsync(Convert.ToInt32(existing),0,0,ct);}
        command.CommandText="SELECT COUNT(*) FROM dbo.Users WITH(HOLDLOCK) WHERE Id IN (SELECT value FROM OPENJSON(@participants))";
        command.Parameters.Add("@participants",SqlDbType.NVarChar).Value=System.Text.Json.JsonSerializer.Serialize(participants);
        if(Convert.ToInt32(await command.ExecuteScalarAsync(ct))!=participants.Length)throw new ArgumentException("Có người tham gia không tồn tại trong hệ thống.");
        command.CommandText="""
            INSERT dbo.Meetings(Title,StartTime,EndTime,LocationOrLink,Description,CreatedByUserId,ClientRequestId)
            OUTPUT INSERTED.Id VALUES(@title,@start,@end,@location,@description,@creator,@requestId)
            """;
        command.Parameters.Add("@title",SqlDbType.NVarChar,200).Value=title;command.Parameters.Add("@start",SqlDbType.DateTimeOffset).Value=request.StartTime;
        command.Parameters.Add("@end",SqlDbType.DateTimeOffset).Value=request.EndTime;
        command.Parameters.Add("@location",SqlDbType.NVarChar,500).Value=(object?)NullIfBlank(request.LocationOrLink)??DBNull.Value;
        command.Parameters.Add("@description",SqlDbType.NVarChar,2000).Value=(object?)NullIfBlank(request.Description)??DBNull.Value;
        var id=Convert.ToInt32(await command.ExecuteScalarAsync(ct));
        command.CommandText="INSERT dbo.MeetingParticipants(MeetingId,UserId) SELECT @meeting,value FROM OPENJSON(@participants)";
        command.Parameters.Add("@meeting",SqlDbType.Int).Value=id;await command.ExecuteNonQueryAsync(ct);await transaction.CommitAsync(ct);
        // The meeting is already committed. Client cancellation must not cancel delivery to its participants.
        var delivery=await NotifyCreatedAsync(id,CancellationToken.None);
        return await LoadResponseAsync(id,delivery.Sent,delivery.Failed,CancellationToken.None);
    }

    public async Task<List<MeetingResponse>> GetAllAsync(CancellationToken ct)
    {await using var c=new SqlConnection(connectionString);await c.OpenAsync(ct);await using var q=new SqlCommand("SELECT Id FROM dbo.Meetings ORDER BY StartTime DESC,Id DESC",c);await using var r=await q.ExecuteReaderAsync(ct);var ids=new List<int>();while(await r.ReadAsync(ct))ids.Add(r.GetInt32(0));var result=new List<MeetingResponse>();foreach(var id in ids)result.Add(await LoadResponseAsync(id,0,0,ct));return result;}
    public async Task<List<MeetingParticipantOption>> GetParticipantsAsync(CancellationToken ct)
    {await using var c=new SqlConnection(connectionString);await c.OpenAsync(ct);await using var q=new SqlCommand("SELECT Id,FullName,Email,Role FROM dbo.Users ORDER BY FullName,Id",c);await using var r=await q.ExecuteReaderAsync(ct);var result=new List<MeetingParticipantOption>();while(await r.ReadAsync(ct))result.Add(new(r.GetInt32(0),r.GetString(1),r.GetString(2),r.GetString(3)));return result;}
    private async Task<MeetingResponse> LoadResponseAsync(int id,int sent,int failed,CancellationToken ct)
    {
        await using var connection=new SqlConnection(connectionString);await connection.OpenAsync(ct);
        await using var command=new SqlCommand("""
            SELECT m.Title,m.StartTime,m.EndTime,m.LocationOrLink,m.Description,m.CreatedByUserId,p.UserId,m.CreatedAt,
                (SELECT COUNT(*) FROM dbo.MeetingEmailDeliveries d WHERE d.MeetingId=m.Id AND d.Status='SENT')
            FROM dbo.Meetings m LEFT JOIN dbo.MeetingParticipants p ON p.MeetingId=m.Id WHERE m.Id=@id ORDER BY p.UserId
            """,connection);
        command.Parameters.Add("@id",SqlDbType.Int).Value=id;
        await using var reader=await command.ExecuteReaderAsync(ct);MeetingResponse? result=null;var people=new List<int>();
        while(await reader.ReadAsync(ct))
        {
            if(!reader.IsDBNull(6))people.Add(reader.GetInt32(6));
            result??=new(id,reader.GetString(0),reader.GetFieldValue<DateTimeOffset>(1),reader.GetFieldValue<DateTimeOffset>(2),
                reader.IsDBNull(3)?null:reader.GetString(3),reader.IsDBNull(4)?null:reader.GetString(4),reader.GetInt32(5),people,
                Math.Max(sent,reader.GetInt32(8)),failed,reader.GetFieldValue<DateTimeOffset>(7));
        }
        return result??throw new KeyNotFoundException("Không tìm thấy lịch họp.");
    }
    public async Task<(int Sent,int Failed)> NotifyCreatedAsync(int meetingId,CancellationToken ct)
    {var meeting=await LoadMeetingAsync(meetingId,ct)??throw new KeyNotFoundException("Không tìm thấy lịch họp.");var sent=0;var failed=0;foreach(var p in meeting.Participants){if(!await TryClaimAsync(meetingId,p.UserId,ct))continue;try{await email.SendAsync(p.Email,EmailTemplates.Meeting(meeting.Title,meeting.StartTime,meeting.EndTime,meeting.LocationOrLink,meeting.Description),ct);await MarkSentAsync(meetingId,p.UserId,ct);sent++;}catch(Exception e)when(e is ArgumentException or PasswordRecoveryUnavailableException or OperationCanceledException){await ReleaseClaimAsync(meetingId,p.UserId,CancellationToken.None);logger.LogWarning("Meeting email failed. MeetingId {MeetingId}, UserId {UserId}, FailureType {FailureType}.",meetingId,p.UserId,e.GetType().Name);failed++;}}return(sent,failed);}
    private async Task<MeetingMailData?> LoadMeetingAsync(int id,CancellationToken ct)
    {await using var c=new SqlConnection(connectionString);await c.OpenAsync(ct);await using var q=new SqlCommand("SELECT m.Title,m.StartTime,m.EndTime,m.LocationOrLink,m.Description,p.UserId,u.Email FROM dbo.Meetings m LEFT JOIN dbo.MeetingParticipants p ON p.MeetingId=m.Id LEFT JOIN dbo.Users u ON u.Id=p.UserId WHERE m.Id=@id ORDER BY p.UserId",c);q.Parameters.Add("@id",SqlDbType.Int).Value=id;await using var r=await q.ExecuteReaderAsync(ct);MeetingMailData? result=null;while(await r.ReadAsync(ct)){result??=new(r.GetString(0),r.GetFieldValue<DateTimeOffset>(1),r.GetFieldValue<DateTimeOffset>(2),r.IsDBNull(3)?null:r.GetString(3),r.IsDBNull(4)?null:r.GetString(4),[]);if(!r.IsDBNull(5))result.Participants.Add(new(r.GetInt32(5),r.GetString(6)));}return result;}
    private async Task<bool> TryClaimAsync(int meetingId,int userId,CancellationToken ct){await using var c=new SqlConnection(connectionString);await c.OpenAsync(ct);await using var q=new SqlCommand("INSERT dbo.MeetingEmailDeliveries(MeetingId,UserId,TriggerType,Status) SELECT @meeting,@user,@trigger,'SENDING' WHERE NOT EXISTS(SELECT 1 FROM dbo.MeetingEmailDeliveries WITH(UPDLOCK,HOLDLOCK) WHERE MeetingId=@meeting AND UserId=@user AND TriggerType=@trigger)",c);Add(q,meetingId,userId);return await q.ExecuteNonQueryAsync(ct)==1;}
    private Task MarkSentAsync(int m,int u,CancellationToken ct)=>DeliveryAsync("UPDATE dbo.MeetingEmailDeliveries SET Status='SENT',SentAt=SYSDATETIMEOFFSET() WHERE MeetingId=@meeting AND UserId=@user AND TriggerType=@trigger",m,u,ct);
    private Task ReleaseClaimAsync(int m,int u,CancellationToken ct)=>DeliveryAsync("DELETE dbo.MeetingEmailDeliveries WHERE MeetingId=@meeting AND UserId=@user AND TriggerType=@trigger AND Status='SENDING'",m,u,ct);
    private async Task DeliveryAsync(string sql,int m,int u,CancellationToken ct){await using var c=new SqlConnection(connectionString);await c.OpenAsync(ct);await using var q=new SqlCommand(sql,c);Add(q,m,u);await q.ExecuteNonQueryAsync(ct);}
    private static void Add(SqlCommand q,int m,int u){q.Parameters.Add("@meeting",SqlDbType.Int).Value=m;q.Parameters.Add("@user",SqlDbType.Int).Value=u;q.Parameters.Add("@trigger",SqlDbType.VarChar,32).Value=CreatedTrigger;}
    private static string? NullIfBlank(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();
    private sealed record Participant(int UserId,string Email);
    private sealed record MeetingMailData(string Title,DateTimeOffset StartTime,DateTimeOffset EndTime,string? LocationOrLink,string? Description,List<Participant> Participants);
}
