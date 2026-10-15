
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

    // Lấy danh sách Intern cùng kết quả đánh giá cuối kỳ mới nhất.
    public async Task<IReadOnlyList<FinalEvaluationResponse>> GetAllAsync(
        CancellationToken cancellationToken)
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
                i.Mentor,

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

            OUTER APPLY (
                SELECT TOP (1)
                    fe.Id,
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
                WHERE fe.InternId = i.Id
                ORDER BY fe.CreatedAt DESC, fe.Id DESC
            ) e

            ORDER BY i.Id DESC;
            """;

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

    // Lấy chi tiết kết quả đánh giá mới nhất theo InternId.
    public async Task<FinalEvaluationResponse?> GetByIdAsync(
        int internId,
        CancellationToken cancellationToken)
    {
        var results = await GetAllAsync(cancellationToken);

        return results.FirstOrDefault(
            x => x.InternId == internId
        );
    }

    // Tổng hợp số lượng và điểm đánh giá cho HR.
    public async Task<FinalEvaluationStatisticsResponse> GetStatisticsAsync(
        CancellationToken cancellationToken)
    {
        var results = await GetAllAsync(cancellationToken);

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

    // THÊM MỚI ĐÁNH GIÁ CUỐI KỲ.
    public async Task<bool> CreateAsync(
        int internId,
        FinalEvaluationRequest request,
        CancellationToken cancellationToken)
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
                Comment
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
                @comment
            FROM dbo.Interns i
            WHERE i.Id = @internId
              AND NOT EXISTS (
                  SELECT 1
                  FROM dbo.FinalEvaluations fe WITH (UPDLOCK, HOLDLOCK)
                  WHERE fe.InternId = i.Id
              );
            """;

        AddEvaluationParameters(command, internId, request);

        var affectedRows =
            await command.ExecuteNonQueryAsync(cancellationToken);

        return affectedRows > 0;
    }

    // CẬP NHẬT ĐÁNH GIÁ CUỐI KỲ MỚI NHẤT.
    public async Task<bool> UpdateAsync(
        int internId,
        FinalEvaluationRequest request,
        CancellationToken cancellationToken)
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
                Comment = @comment
            WHERE Id = (
                SELECT TOP (1) Id
                FROM dbo.FinalEvaluations
                WHERE InternId = @internId
                ORDER BY CreatedAt DESC, Id DESC
            );
            """;

        AddEvaluationParameters(command, internId, request);

        var affectedRows =
            await command.ExecuteNonQueryAsync(cancellationToken);

        return affectedRows > 0;
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

    // Đọc dữ liệu chuỗi, xử lý giá trị NULL.
    private static string GetString(
        SqlDataReader reader,
        string column)
    {
        var ordinal = reader.GetOrdinal(column);

        return reader.IsDBNull(ordinal)
            ? string.Empty
            : reader.GetString(ordinal);
    }

    // Đọc dữ liệu điểm số, xử lý giá trị NULL.
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
