using System.Data;
using InternManagement.DTOs;
using Microsoft.Data.SqlClient;

namespace InternManagement.Services;

public sealed class InternAssignmentService(IConfiguration configuration)
{
    private readonly string connectionString = configuration.GetConnectionString("InternManagement")
        ?? throw new InvalidOperationException("Chưa cấu hình cơ sở dữ liệu.");

    public async Task<List<MentorOption>> GetMentorsAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("SELECT Id, FullName, Email FROM dbo.Users WHERE Role = 'MENTOR' ORDER BY FullName, Id", connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<MentorOption>();
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new(reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
        return result;
    }

    public async Task<List<MentorLoadItem>> GetMentorLoadAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("""
            SELECT u.Id, u.FullName, u.Email, COUNT(i.Id) AS InternCount
            FROM dbo.Users u
            LEFT JOIN dbo.Interns i ON i.MentorUserId = u.Id
            WHERE u.Role = 'MENTOR'
            GROUP BY u.Id, u.FullName, u.Email
            ORDER BY u.FullName, u.Id
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<MentorLoadItem>();
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3)));
        return result;
    }

    public async Task<List<InternAssignmentResponse>> GetInternsAsync(bool assignedOnly, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("""
            SELECT i.Id, i.FullName, i.Email, i.MentorUserId, m.FullName, i.InternshipProgramId, i.Status
            FROM dbo.Interns i LEFT JOIN dbo.Users m ON m.Id = i.MentorUserId AND m.Role = 'MENTOR'
            WHERE @AssignedOnly = 0 OR i.MentorUserId IS NOT NULL
            ORDER BY i.FullName, i.Id
            """, connection);
        command.Parameters.Add("@AssignedOnly", SqlDbType.Bit).Value = assignedOnly;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<InternAssignmentResponse>();
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new(reader.GetInt32(0), reader.GetString(1), reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetInt32(3), reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetInt32(5), reader.IsDBNull(6) ? null : reader.GetString(6)));
        return result;
    }

    // Updating the existing Intern row makes repeated assignments idempotent, including concurrent saves.
    public async Task AssignAsync(int internId, AssignMentorRequest request, CancellationToken cancellationToken, bool allowCreate=true, bool allowUpdate=true)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new SqlCommand("""
            SELECT CASE
                WHEN NOT EXISTS (SELECT 1 FROM dbo.Interns WITH (UPDLOCK, HOLDLOCK) WHERE Id = @InternId) THEN 1
                WHEN EXISTS(SELECT 1 FROM dbo.Interns WHERE Id=@InternId AND
                    ((MentorUserId IS NULL AND @Create=0) OR (MentorUserId IS NOT NULL AND @Update=0))) THEN 5
                WHEN NOT EXISTS (SELECT 1 FROM dbo.Users WITH (HOLDLOCK) WHERE Id = @MentorId AND Role = 'MENTOR') THEN 2
                WHEN @ProgramId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.InternshipPrograms WITH (HOLDLOCK) WHERE Id = @ProgramId) THEN 3
                WHEN @ProgramId IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.Interns WHERE Id = @InternId
                    AND MentorUserId IS NOT NULL AND InternshipProgramId IS NOT NULL AND InternshipProgramId <> @ProgramId) THEN 8
                WHEN NOT EXISTS (SELECT 1 FROM dbo.InternDocuments WITH (HOLDLOCK) WHERE InternId=@InternId AND Kind='application' AND ReviewStatus='approved')
                    OR EXISTS (SELECT 1 FROM dbo.InternDocuments WITH (HOLDLOCK) WHERE InternId=@InternId AND ReviewStatus='rejected') THEN 6
                WHEN NOT EXISTS(SELECT 1 FROM dbo.InternContracts WITH(HOLDLOCK) WHERE InternId=@InternId)
                    OR (@ProgramId IS NULL AND NOT EXISTS(SELECT 1 FROM dbo.Interns WHERE Id=@InternId AND InternshipProgramId IS NOT NULL)) THEN 7
                WHEN EXISTS (SELECT 1 FROM dbo.Interns WHERE Id = @InternId
                    AND NULLIF(LTRIM(RTRIM(Status)), N'') IS NOT NULL
                    AND Status NOT IN (N'Chờ tiếp nhận', N'Đang thực tập')) THEN 4
                ELSE 0 END
            """, connection, transaction);
        command.Parameters.Add("@InternId", SqlDbType.Int).Value = internId;
        command.Parameters.Add("@MentorId", SqlDbType.Int).Value = request.MentorUserId;
        command.Parameters.Add("@ProgramId", SqlDbType.Int).Value = (object?)request.InternshipProgramId ?? DBNull.Value;
        command.Parameters.Add("@Create",SqlDbType.Bit).Value=allowCreate;command.Parameters.Add("@Update",SqlDbType.Bit).Value=allowUpdate;
        var validation = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        if(validation==5)throw new UnauthorizedAccessException();
        if (validation != 0)
            throw new AssignmentValidationException(validation == 1 ? "Không tìm thấy thực tập sinh."
                : validation == 2 ? "Tài khoản Mentor không tồn tại hoặc không có vai trò MENTOR."
                : validation == 3 ? "Không tìm thấy chương trình thực tập."
                : validation == 8 ? "Đổi Mentor phải giữ chương trình thực tập hiện tại."
                : validation == 6 ? "Hồ sơ chưa được duyệt hoặc có tài liệu bị từ chối."
                : validation == 7 ? "Cần hợp đồng và chương trình thực tập trước khi phân công."
                : "Trạng thái thực tập sinh không cho phép phân công Mentor.", validation == 1);
        command.CommandText = "UPDATE dbo.Interns SET MentorUserId = @MentorId, InternshipProgramId = COALESCE(@ProgramId,InternshipProgramId), Status = CASE WHEN MentorUserId IS NULL THEN N'Đang thực tập' ELSE Status END WHERE Id = @InternId";
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(int internId,CancellationToken ct)
    {
        await using var connection=new SqlConnection(connectionString);await connection.OpenAsync(ct);
        await using var command=new SqlCommand("UPDATE dbo.Interns SET MentorUserId=NULL WHERE Id=@id AND MentorUserId IS NOT NULL",connection);
        command.Parameters.Add("@id",SqlDbType.Int).Value=internId;
        return await command.ExecuteNonQueryAsync(ct)==1;
    }

    public async Task<InternScheduleResponse?> GetOwnScheduleAsync(int userId, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("""
            SELECT i.Id, p.Id, p.StartDate, p.EndDate, COALESCE(p.Department,i.Department), m.FullName, p.Name
            FROM dbo.Users u
            INNER JOIN dbo.Interns i ON i.Email = u.Email
            LEFT JOIN dbo.InternshipPrograms p ON p.Id = i.InternshipProgramId
            LEFT JOIN dbo.Users m ON m.Id = i.MentorUserId AND m.Role = 'MENTOR'
            WHERE u.Id = @UserId
            """, connection);
        command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        DateOnly? start = reader.IsDBNull(2) ? null : DateOnly.FromDateTime(reader.GetDateTime(2));
        DateOnly? end = reader.IsDBNull(3) ? null : DateOnly.FromDateTime(reader.GetDateTime(3));
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).Date);
        var status = start is null || end is null ? null : today < start ? "UPCOMING" : today > end ? "ENDED" : "ACTIVE";
        return new(reader.GetInt32(0), reader.IsDBNull(1) ? null : reader.GetInt32(1), start, end,
            reader.IsDBNull(4) ? null : reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5), status,
            reader.IsDBNull(6) ? null : reader.GetString(6), start is not null && end is not null ? end.Value.DayNumber-start.Value.DayNumber+1 : null);
    }
}

public sealed class AssignmentValidationException(string message, bool notFound) : Exception(message)
{
    public bool NotFound { get; } = notFound;
}
