using System.Data;
using InternManagement.DTOs;
using Microsoft.Data.SqlClient;

namespace InternManagement.Services;

public sealed class InternTaskService(IConfiguration configuration, TimeProvider clock)
{
    private readonly string connectionString = configuration.GetConnectionString("InternManagement")
        ?? throw new InvalidOperationException("Chưa cấu hình cơ sở dữ liệu.");

    public async Task<List<TaskInternOption>> GetInternsAsync(int mentorId, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand("SELECT Id,FullName,Email FROM dbo.Interns WHERE MentorUserId=@id ORDER BY FullName,Id", connection);
        command.Parameters.Add("@id", SqlDbType.Int).Value = mentorId;
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<TaskInternOption>();
        while (await reader.ReadAsync(ct)) result.Add(new(reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
        return result;
    }

    public async Task<int?> CreateAsync(int mentorId, CreateInternTaskRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Content) || request.Content.Length > 2000 || request.Deadline <= clock.GetUtcNow())
            throw new ArgumentException("Nội dung là bắt buộc (tối đa 2000 ký tự) và deadline phải ở tương lai.");
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);
        // Lock the existing assignment row until insert commits; reassignment cannot race this check.
        await using var command = new SqlCommand("""
            INSERT dbo.InternTasks(InternId,MentorUserId,Content,Deadline)
            OUTPUT INSERTED.Id
            SELECT i.Id,@mentor,@content,@deadline FROM dbo.Interns i WITH(UPDLOCK,HOLDLOCK)
            WHERE i.Id=@intern AND i.MentorUserId=@mentor
                AND EXISTS(SELECT 1 FROM dbo.Users u WITH(HOLDLOCK) WHERE u.Id=@mentor AND u.Role='MENTOR');
            """, connection, transaction);
        command.Parameters.Add("@mentor", SqlDbType.Int).Value = mentorId;
        command.Parameters.Add("@intern", SqlDbType.Int).Value = request.InternId;
        command.Parameters.Add("@content", SqlDbType.NVarChar, 2000).Value = request.Content.Trim();
        command.Parameters.Add("@deadline", SqlDbType.DateTimeOffset).Value = request.Deadline;
        var id = await command.ExecuteScalarAsync(ct);
        if (id is not null) {
            command.CommandText = """
                INSERT dbo.Notifications(UserId,Title,Message,Category,Type,ActionUrl,CreatedBy)
                SELECT u.Id,N'Nhiệm vụ mới',@content,'ASSIGNMENT','TASK_ASSIGNED','tasks.html',@mentor
                FROM dbo.Interns i JOIN dbo.Users u ON u.Email=i.Email AND u.Role='INTERN'
                WHERE i.Id=@intern;
                """;
            // Notification messages have a smaller limit than task content.
            command.Parameters["@content"].Value = request.Content.Trim()[..Math.Min(1000,request.Content.Trim().Length)];
            await command.ExecuteNonQueryAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return id is null ? null : Convert.ToInt32(id);
    }

    public async Task<List<InternTaskResponse>> GetAsync(int userId, bool mentor, int? taskId, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand("""
            SELECT t.Id,t.InternId,i.FullName,t.MentorUserId,m.FullName,t.Content,t.Deadline,t.Status,t.CreatedAt,
                t.Progress,t.ProgressNote,t.UpdatedAt
            FROM dbo.InternTasks t
            JOIN dbo.Interns i ON i.Id=t.InternId JOIN dbo.Users m ON m.Id=t.MentorUserId
            WHERE (@task IS NULL OR t.Id=@task) AND
                ((@mentor=1 AND i.MentorUserId=@user) OR
                 (@mentor=0 AND EXISTS(SELECT 1 FROM dbo.Users u WHERE u.Id=@user AND u.Email=i.Email AND u.Role='INTERN')))
            ORDER BY t.Id DESC;
            """, connection);
        command.Parameters.Add("@user", SqlDbType.Int).Value = userId;
        command.Parameters.Add("@mentor", SqlDbType.Bit).Value = mentor;
        command.Parameters.Add("@task", SqlDbType.Int).Value = (object?)taskId ?? DBNull.Value;
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<InternTaskResponse>();
        while (await reader.ReadAsync(ct)) result.Add(new(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2),
            reader.GetInt32(3), reader.GetString(4), reader.GetString(5), reader.GetFieldValue<DateTimeOffset>(6),
            reader.GetString(7), reader.GetFieldValue<DateTimeOffset>(8), reader.GetInt32(9),
            reader.IsDBNull(10) ? null : reader.GetString(10),
            reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTimeOffset>(11)));
        return result;
    }

    public async Task<bool> UpdateProgressAsync(int userId, int taskId, UpdateTaskProgressRequest request, CancellationToken ct)
    {
        if (request.Progress is null or < 0 or > 100 || request.ProgressNote?.Length > 2000)
            throw new ArgumentException("Tiến độ phải từ 0 đến 100; ghi chú tối đa 2000 ký tự.");
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand("""
            UPDATE t SET Progress=@progress, ProgressNote=@note, UpdatedAt=SYSDATETIMEOFFSET(),
                Status=CASE WHEN @progress=100 THEN 'COMPLETED' WHEN @progress=0 THEN 'ASSIGNED' ELSE 'IN_PROGRESS' END
            FROM dbo.InternTasks t JOIN dbo.Interns i ON i.Id=t.InternId
            WHERE t.Id=@task AND EXISTS(
                SELECT 1 FROM dbo.Users u WHERE u.Id=@user AND u.Role='INTERN' AND u.Email=i.Email);
            """, connection);
        command.Parameters.Add("@task", SqlDbType.Int).Value = taskId;
        command.Parameters.Add("@user", SqlDbType.Int).Value = userId;
        command.Parameters.Add("@progress", SqlDbType.Int).Value = request.Progress.Value;
        command.Parameters.Add("@note", SqlDbType.NVarChar, 2000).Value = (object?)request.ProgressNote?.Trim() ?? DBNull.Value;
        return await command.ExecuteNonQueryAsync(ct) > 0;
    }
}
