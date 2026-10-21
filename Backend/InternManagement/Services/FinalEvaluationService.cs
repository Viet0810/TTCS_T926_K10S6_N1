
using InternManagement.DTOs;
using Microsoft.Data.SqlClient;
using System.Data;

namespace InternManagement.Services;

public sealed class FinalEvaluationService
{
    private readonly string connectionString;

    public FinalEvaluationService(IConfiguration configuration)
    {
        connectionString =
            configuration.GetConnectionString("InternManagement")
            ?? throw new InvalidOperationException(
                "Chưa cấu hình connection string InternManagement."
            );
    }

    // Lấy danh sách thực tập sinh và đánh giá mới nhất.
    public async Task<IReadOnlyList<FinalEvaluationResponse>> GetAllAsync(
        CancellationToken cancellationToken,
        int? mentorUserId = null)
    {
        var results = new List<FinalEvaluationResponse>();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT
                i.Id AS InternId,
                i.StudentCode,
                i.FullName AS Name,
                i.ClassName,
                i.Organization AS Company,
                COALESCE(mu.FullName,i.Mentor) AS Mentor, p.Name AS ProgramName,
                me.FinalScore AS MentorScore,me.Comment AS MentorComment,me.CreatedByUserId AS EvaluatingMentorId,
                me.Period AS MentorPeriod,me.Attendance AS MentorAttendance,me.Attitude AS MentorAttitude,
                me.ProfessionalSkills AS MentorProfessionalSkills,me.WorkResult AS MentorWorkResult,
                me.Classification AS MentorClassification,
                e.EvaluationSource,

                ISNULL(e.Period, '') AS Period,
                e.Attendance,
                e.Attitude,
                e.ProfessionalSkills,
                e.WorkResult,
                e.FinalScore,

                ISNULL(e.Classification, '') AS Classification,

                CASE
                    WHEN e.Id IS NOT NULL
                         AND e.Status = N'Đã đánh giá'
                    THEN N'Đã đánh giá'
                    ELSE N'Chưa đánh giá'
                END AS Status,

                ISNULL(e.Comment, '') AS Comment

            FROM dbo.Interns i
            LEFT JOIN dbo.Users mu ON mu.Id=i.MentorUserId
            LEFT JOIN dbo.InternshipPrograms p ON p.Id=i.InternshipProgramId

            OUTER APPLY (
                SELECT TOP (1)
                    fe.Id,fe.EvaluationSource,
                    fe.Period,
                    fe.Attendance,
                    fe.Attitude,
                    fe.ProfessionalSkills,
                    fe.WorkResult,
                    fe.FinalScore,
                    fe.Classification,
                    fe.Status,
                    fe.Comment
                FROM dbo.FinalEvaluations fe
                WHERE fe.InternId = i.Id AND
                    ((@mentorUserId IS NULL AND fe.EvaluationSource IN ('HR','LEGACY'))
                     OR (@mentorUserId IS NOT NULL AND fe.EvaluationSource='MENTOR' AND fe.CreatedByUserId=@mentorUserId))
                ORDER BY CASE WHEN @mentorUserId IS NULL AND fe.EvaluationSource='HR' THEN 0 ELSE 1 END,fe.CreatedAt DESC, fe.Id DESC
            ) e
            OUTER APPLY(SELECT TOP(1) Period,Attendance,Attitude,ProfessionalSkills,WorkResult,FinalScore,Classification,Comment,CreatedByUserId FROM dbo.FinalEvaluations WHERE InternId=i.Id AND EvaluationSource='MENTOR' ORDER BY CreatedAt DESC,Id DESC) me

            WHERE (@mentorUserId IS NULL OR i.MentorUserId = @mentorUserId)
            ORDER BY i.Id DESC;
            """;

        command.Parameters.Add("@mentorUserId", SqlDbType.Int).Value = (object?)mentorUserId ?? DBNull.Value;

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new FinalEvaluationResponse
            {
                InternId = reader.GetInt32(
                    reader.GetOrdinal("InternId")
                ),

                StudentCode = GetString(reader, "StudentCode"),
                Name = GetString(reader, "Name"),
                ClassName = GetString(reader, "ClassName"),
                Company = GetString(reader, "Company"),
                Mentor = GetString(reader, "Mentor"),
                ProgramName=GetString(reader,"ProgramName"),MentorScore=GetDecimal(reader,"MentorScore"),MentorComment=GetString(reader,"MentorComment"),EvaluationSource=GetString(reader,"EvaluationSource"),
                MentorPeriod=GetString(reader,"MentorPeriod"),MentorAttendance=GetDecimal(reader,"MentorAttendance"),MentorAttitude=GetDecimal(reader,"MentorAttitude"),
                MentorProfessionalSkills=GetDecimal(reader,"MentorProfessionalSkills"),MentorWorkResult=GetDecimal(reader,"MentorWorkResult"),MentorClassification=GetString(reader,"MentorClassification"),
                EvaluatingMentorId=reader.IsDBNull(reader.GetOrdinal("EvaluatingMentorId"))?null:reader.GetInt32(reader.GetOrdinal("EvaluatingMentorId")),
                Period = GetString(reader, "Period"),

                Attendance = GetDecimal(reader, "Attendance"),
                Attitude = GetDecimal(reader, "Attitude"),
                ProfessionalSkills = GetDecimal(
                    reader, "ProfessionalSkills"
                ),

                WorkResult = GetDecimal(reader, "WorkResult"),
                FinalScore = GetDecimal(reader, "FinalScore"),

                Classification = GetString(
                    reader, "Classification"
                ),

                Status = GetString(reader, "Status"),
                Comment = GetString(reader, "Comment")
            });
        }

        return results;
    }

    // Xác định InternId từ tài khoản đăng nhập.
    // Liên kết Users và Interns bằng Email.
    public async Task<int?> GetInternIdByUserIdAsync(
        int userId,
        CancellationToken cancellationToken)
    {
        await using var connection =
            new SqlConnection(connectionString);

        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT i.Id
            FROM dbo.Users AS u
            INNER JOIN dbo.Interns AS i
                ON i.Email = u.Email
            WHERE u.Id = @userId
              AND u.Role = 'INTERN';
            """;

        command.Parameters.Add("@userId", SqlDbType.Int)
            .Value = userId;

        var result = await command.ExecuteScalarAsync(
            cancellationToken
        );

        return result is null || result == DBNull.Value
            ? null
            : Convert.ToInt32(result);
    }

    // Lấy đánh giá của một thực tập sinh.
    // InternId phải được Backend xác minh trước khi gọi.
    public async Task<IReadOnlyList<FinalEvaluationResponse>> GetOwnAsync(
        int internId,
        CancellationToken cancellationToken)
    {
        var evaluation = await GetByIdAsync(
            internId,
            cancellationToken
        );

        if (evaluation is null)
        {
            return Array.Empty<FinalEvaluationResponse>();
        }

        return new[] { evaluation };
    }

    // Lấy chi tiết đánh giá mới nhất theo InternId.
    public async Task<FinalEvaluationResponse?> GetByIdAsync(
        int internId,
        CancellationToken cancellationToken,
        int? mentorUserId = null)
    {
        var results = await GetAllAsync(cancellationToken, mentorUserId);

        return results.FirstOrDefault(
            x => x.InternId == internId
        );
    }

    // Thống kê đánh giá cuối kỳ.
    public async Task<FinalEvaluationStatisticsResponse> GetStatisticsAsync(
        CancellationToken cancellationToken,
        int? mentorUserId = null)
    {
        var results = await GetAllAsync(cancellationToken, mentorUserId);

        var evaluated = results
            .Where(x => x.Status == "Đã đánh giá")
            .ToList();

        var scores = evaluated
            .Where(x => x.FinalScore.HasValue)
            .Select(x => x.FinalScore!.Value)
            .ToList();

        return new FinalEvaluationStatisticsResponse
        {
            TotalInterns = results.Count,
            EvaluatedCount = evaluated.Count,
            PendingCount = results.Count - evaluated.Count,
            AverageScore = scores.Count > 0
                ? Math.Round(scores.Average(), 2)
                : null
        };
    }

    // Thêm đánh giá cuối kỳ.
    public async Task<bool> CreateAsync(
        int internId,
        FinalEvaluationRequest request,
        CancellationToken cancellationToken,
        int? mentorUserId = null, int? actorUserId = null)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();

        command.CommandText = """
            INSERT INTO dbo.FinalEvaluations
            (
                InternId,
                Period,
                Attendance,
                Attitude,
                ProfessionalSkills,
                WorkResult,
                FinalScore,
                Classification,
                Status,
                Comment,CreatedByUserId,EvaluationSource
            )
            SELECT
                i.Id,
                @period,
                @attendance,
                @attitude,
                @professionalSkills,
                @workResult,
                @finalScore,
                @classification,
                N'Đã đánh giá',
                @comment,@actor,CASE WHEN @mentorUserId IS NULL THEN 'HR' ELSE 'MENTOR' END
            FROM dbo.Interns i WITH (UPDLOCK, HOLDLOCK)
            WHERE i.Id = @internId
              AND (@mentorUserId IS NULL OR i.MentorUserId = @mentorUserId)
              AND NOT EXISTS (
                  SELECT 1
                  FROM dbo.FinalEvaluations fe WITH (UPDLOCK, HOLDLOCK)
                  WHERE fe.InternId = i.Id AND ((@mentorUserId IS NULL AND fe.EvaluationSource='HR') OR (@mentorUserId IS NOT NULL AND fe.CreatedByUserId=@mentorUserId AND fe.EvaluationSource='MENTOR'))
              );
            """;

        AddEvaluationParameters(command, internId, request);
        command.Parameters.Add("@actor",SqlDbType.Int).Value=(object?)(actorUserId??mentorUserId)??DBNull.Value;
        command.Parameters.Add("@mentorUserId", SqlDbType.Int).Value = (object?)mentorUserId ?? DBNull.Value;

        var affectedRows =
            await command.ExecuteNonQueryAsync(cancellationToken);

        return affectedRows > 0;
    }

    // Cập nhật đánh giá cuối kỳ mới nhất.
    public async Task<bool> UpdateAsync(
        int internId,
        FinalEvaluationRequest request,
        CancellationToken cancellationToken,
        int? mentorUserId = null, int? actorUserId = null)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();

        command.CommandText = """
            UPDATE dbo.FinalEvaluations
            SET
                Period = @period,
                Attendance = @attendance,
                Attitude = @attitude,
                ProfessionalSkills = @professionalSkills,
                WorkResult = @workResult,
                FinalScore = @finalScore,
                Classification = @classification,
                Status = N'Đã đánh giá',
                Comment = @comment,UpdatedAt=SYSDATETIMEOFFSET()
            WHERE (@mentorUserId IS NULL OR EXISTS (
                SELECT 1 FROM dbo.Interns i WITH (UPDLOCK, HOLDLOCK)
                WHERE i.Id = @internId AND i.MentorUserId = @mentorUserId
            ))
            AND Id = (
                SELECT TOP (1) Id
                FROM dbo.FinalEvaluations
                WHERE InternId = @internId AND ((@mentorUserId IS NULL AND EvaluationSource='HR') OR (@mentorUserId IS NOT NULL AND CreatedByUserId=@mentorUserId AND EvaluationSource='MENTOR'))
                ORDER BY CreatedAt DESC, Id DESC
            );
            """;

        AddEvaluationParameters(command, internId, request);
        command.Parameters.Add("@actor",SqlDbType.Int).Value=(object?)(actorUserId??mentorUserId)??DBNull.Value;
        command.Parameters.Add("@mentorUserId", SqlDbType.Int).Value = (object?)mentorUserId ?? DBNull.Value;

        var affectedRows =
            await command.ExecuteNonQueryAsync(cancellationToken);

        return affectedRows > 0 || await CreateAsync(internId,request,cancellationToken,mentorUserId,actorUserId);
    }

    // Gán tham số SQL, tránh SQL Injection.
    private static void AddEvaluationParameters(
        SqlCommand command,
        int internId,
        FinalEvaluationRequest request)
    {
        command.Parameters.Add("@internId", SqlDbType.Int)
            .Value = internId;

        command.Parameters.Add("@period", SqlDbType.NVarChar, 200)
            .Value = request.Period.Trim();

        command.Parameters.Add("@attendance", SqlDbType.Decimal)
            .Value = request.Attendance;

        command.Parameters.Add("@attitude", SqlDbType.Decimal)
            .Value = request.Attitude;

        command.Parameters.Add("@professionalSkills", SqlDbType.Decimal)
            .Value = request.ProfessionalSkills;

        command.Parameters.Add("@workResult", SqlDbType.Decimal)
            .Value = request.WorkResult;

        command.Parameters.Add("@finalScore", SqlDbType.Decimal)
            .Value = request.FinalScore;

        command.Parameters.Add("@classification", SqlDbType.NVarChar, 50)
            .Value = request.Classification.Trim();

        command.Parameters.Add("@comment", SqlDbType.NVarChar, -1)
            .Value = (object?)request.Comment?.Trim()
                ?? DBNull.Value;
    }

    // Đọc dữ liệu chuỗi, xử lý NULL.
    private static string GetString(
        SqlDataReader reader,
        string column)
    {
        var ordinal = reader.GetOrdinal(column);

        return reader.IsDBNull(ordinal)
            ? string.Empty
            : reader.GetString(ordinal);
    }

    // Đọc dữ liệu điểm số, xử lý NULL.
    private static decimal? GetDecimal(
        SqlDataReader reader,
        string column)
    {
        var ordinal = reader.GetOrdinal(column);

        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetDecimal(ordinal);
    }
}
