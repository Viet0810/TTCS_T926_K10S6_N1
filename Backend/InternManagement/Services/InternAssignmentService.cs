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
    public async Task AssignAsync(int internId, AssignMentorRequest request, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new SqlCommand("""
            SELECT CASE
                WHEN NOT EXISTS (SELECT 1 FROM dbo.Interns WITH (UPDLOCK, HOLDLOCK) WHERE Id = @InternId) THEN 1
                WHEN NOT EXISTS (SELECT 1 FROM dbo.Users WITH (HOLDLOCK) WHERE Id = @MentorId AND Role = 'MENTOR') THEN 2
                WHEN @ProgramId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.InternshipPrograms WITH (HOLDLOCK) WHERE Id = @ProgramId) THEN 3
                WHEN EXISTS (SELECT 1 FROM dbo.Interns WHERE Id = @InternId
                    AND NULLIF(LTRIM(RTRIM(Status)), N'') IS NOT NULL
                    AND Status NOT IN (N'Chờ tiếp nhận', N'Đang thực tập')) THEN 4
                ELSE 0 END
            """, connection, transaction);
        command.Parameters.Add("@InternId", SqlDbType.Int).Value = internId;
        command.Parameters.Add("@MentorId", SqlDbType.Int).Value = request.MentorUserId;
        command.Parameters.Add("@ProgramId", SqlDbType.Int).Value = (object?)request.InternshipProgramId ?? DBNull.Value;
        var validation = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        if (validation != 0)
            throw new AssignmentValidationException(validation == 1 ? "Không tìm thấy thực tập sinh."
                : validation == 2 ? "Tài khoản Mentor không tồn tại hoặc không có vai trò MENTOR."
                : validation == 3 ? "Không tìm thấy chương trình thực tập."
                : "Trạng thái thực tập sinh không cho phép phân công Mentor.", validation == 1);
        command.CommandText = "UPDATE dbo.Interns SET MentorUserId = @MentorId, InternshipProgramId = @ProgramId, Status = N'Đang thực tập' WHERE Id = @InternId";
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<InternScheduleResponse?> GetOwnScheduleAsync(int userId, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("""
            SELECT i.Id, p.Id, p.StartDate, p.EndDate, COALESCE(p.Department,i.Department), m.FullName
            FROM dbo.Users u
            INNER JOIN dbo.Interns i ON i.Email = u.Email
            LEFT JOIN dbo.InternshipPrograms p ON p.Id = i.InternshipProgramId
            LEFT JOIN dbo.Users m ON m.Id = i.MentorUserId AND m.Role = 'MENTOR'
            WHERE u.Id = @UserId AND u.Role = 'INTERN'
            """, connection);
        command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        DateOnly? start = reader.IsDBNull(2) ? null : DateOnly.FromDateTime(reader.GetDateTime(2));
        DateOnly? end = reader.IsDBNull(3) ? null : DateOnly.FromDateTime(reader.GetDateTime(3));
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).Date);
        var status = start is null || end is null ? null : today < start ? "UPCOMING" : today > end ? "ENDED" : "ACTIVE";
        return new(reader.GetInt32(0), reader.IsDBNull(1) ? null : reader.GetInt32(1), start, end,
            reader.IsDBNull(4) ? null : reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5), status);
    }
}

public sealed class AssignmentValidationException(string message, bool notFound) : Exception(message)
{
    public bool NotFound { get; } = notFound;
}
