using InternManagement.DTOs;
using Npgsql;
using NpgsqlTypes;

namespace InternManagement.Services;

public sealed class WeeklyReportService(IConfiguration configuration)
{
    private readonly string connectionString = configuration.GetConnectionString("InternManagement")
        ?? throw new InvalidOperationException("Chưa cấu hình ConnectionStrings:InternManagement.");

    public async Task<int?> GetInternIdForUserAsync(int userId, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT i.id FROM dbo.interns i
            JOIN dbo.users u ON lower(u.email) = lower(i.email)
            WHERE u.id = @userId
            """;
        command.Parameters.AddWithValue("userId", userId);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? null : Convert.ToInt32(result);
    }

    public async Task<bool> HasAssignedMentorAsync(int internId, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS (
                SELECT 1 FROM dbo.interns i
                JOIN dbo.users mentor ON lower(mentor.email) = lower(i.mentoremail)
                    AND mentor.role = 'MENTOR'
                WHERE i.id = @internId
            )
            """;
        command.Parameters.AddWithValue("internId", internId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public async Task<IReadOnlyList<WeeklyReportResponse>> ListForInternAsync(int internId, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT r.id, i.id, i.fullname, i.studentcode, i.organization,
                   r.weekstart, r.weekend, r.summary, r.accomplishments, r.challenges,
                   r.nextweekplan, r.submittedat, r.mentorfeedback, r.feedbackat
            FROM dbo.weeklyreports r
            JOIN dbo.interns i ON i.id = r.internid
            WHERE i.id = @internId
            ORDER BY r.weekstart DESC, r.id DESC
            """;
        command.Parameters.AddWithValue("internId", internId);
        return await ReadReportsAsync(command, cancellationToken);
    }

    public async Task<long?> SaveForInternAsync(int internId, CreateWeeklyReportRequest request, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO dbo.weeklyreports AS current_report
                (internid, weekstart, weekend, summary, accomplishments, challenges, nextweekplan)
            VALUES (@internId, @weekStart, @weekEnd, @summary, @accomplishments, @challenges, @nextWeekPlan)
            ON CONFLICT (internid, weekstart) DO UPDATE SET
                weekend = EXCLUDED.weekend,
                summary = EXCLUDED.summary,
                accomplishments = EXCLUDED.accomplishments,
                challenges = EXCLUDED.challenges,
                nextweekplan = EXCLUDED.nextweekplan,
                submittedat = CURRENT_TIMESTAMP
            WHERE current_report.mentorfeedback IS NULL
            RETURNING current_report.id
            """;
        command.Parameters.AddWithValue("internId", internId);
        command.Parameters.Add("weekStart", NpgsqlDbType.Date).Value = request.WeekStart!.Value;
        command.Parameters.Add("weekEnd", NpgsqlDbType.Date).Value = request.WeekEnd!.Value;
        command.Parameters.Add("summary", NpgsqlDbType.Varchar, 4000).Value = request.Summary.Trim();
        AddOptional(command, "accomplishments", request.Accomplishments, 4000);
        AddOptional(command, "challenges", request.Challenges, 4000);
        AddOptional(command, "nextWeekPlan", request.NextWeekPlan, 4000);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? null : (long)result;
    }

    public async Task<IReadOnlyList<WeeklyReportResponse>> ListForMentorAsync(int mentorId, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT r.id, i.id, i.fullname, i.studentcode, i.organization,
                   r.weekstart, r.weekend, r.summary, r.accomplishments, r.challenges,
                   r.nextweekplan, r.submittedat, r.mentorfeedback, r.feedbackat
            FROM dbo.weeklyreports r
            JOIN dbo.interns i ON i.id = r.internid
            JOIN dbo.users mentor ON lower(mentor.email) = lower(i.mentoremail)
            WHERE mentor.id = @mentorId
            ORDER BY r.weekstart DESC, i.fullname, r.id DESC
            """;
        command.Parameters.AddWithValue("mentorId", mentorId);
        return await ReadReportsAsync(command, cancellationToken);
    }

    private static async Task<IReadOnlyList<WeeklyReportResponse>> ReadReportsAsync(NpgsqlCommand command, CancellationToken cancellationToken)
    {
        var reports = new List<WeeklyReportResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            reports.Add(new WeeklyReportResponse(
                reader.GetInt64(0), reader.GetInt32(1), reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetFieldValue<DateOnly>(5), reader.GetFieldValue<DateOnly>(6),
                reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9), reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.GetFieldValue<DateTimeOffset>(11), reader.IsDBNull(12) ? null : reader.GetString(12),
                reader.IsDBNull(13) ? null : reader.GetFieldValue<DateTimeOffset>(13)));
        }
        return reports;
    }

    private static void AddOptional(NpgsqlCommand command, string name, string? value, int length) =>
        command.Parameters.Add(name, NpgsqlDbType.Varchar, length).Value =
            string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

    public async Task<bool> SaveFeedbackAsync(long reportId, int mentorId, string feedback, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE dbo.weeklyreports r
            SET mentorfeedback = @feedback, feedbackat = CURRENT_TIMESTAMP, feedbackby = @mentorId
            FROM dbo.interns i, dbo.users mentor
            WHERE r.id = @reportId AND i.id = r.internid
              AND mentor.id = @mentorId
              AND lower(mentor.email) = lower(i.mentoremail)
            """;
        command.Parameters.AddWithValue("reportId", reportId);
        command.Parameters.AddWithValue("mentorId", mentorId);
        command.Parameters.Add("feedback", NpgsqlDbType.Varchar, 2000).Value = feedback.Trim();
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }
}
