using System.Data;
using System.Globalization;
using System.Text;
using InternManagement.DTOs;
using Microsoft.Data.SqlClient;

namespace InternManagement.Services;

public sealed class EvaluationSummaryService(IConfiguration configuration)
{
    private readonly string connectionString = configuration.GetConnectionString("InternManagement")
        ?? throw new InvalidOperationException("Chưa cấu hình cơ sở dữ liệu.");

    public async Task<PageResponse<EvaluationSummaryListItem>> GetPageAsync(
        int page, int pageSize, string? search, int? programId, int? mentorId,
        string? status, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var where = BuildWhere(search, programId, mentorId, status);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        AddFilters(command, search, programId, mentorId, status);
        command.CommandText = $"SELECT COUNT(*) FROM dbo.Interns i LEFT JOIN dbo.EvaluationSummaries s ON s.InternId=i.Id {where}";
        var total = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        var pages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
        var current = Math.Min(page, Math.Max(1, pages));
        command.Parameters.Add("@offset", SqlDbType.Int).Value = (current - 1) * pageSize;
        command.Parameters.Add("@pageSize", SqlDbType.Int).Value = pageSize;
        command.CommandText = $"""
            SELECT i.Id,i.StudentCode,i.FullName,i.Email,i.MentorUserId,m.FullName,
                   i.InternshipProgramId,p.Name,
                   scores.EvaluationCount,COALESCE(s.RequiredEvaluationCount,1),scores.AverageScore,
                   COALESCE(classificationRule.Label,N'Chưa có'),
                   CASE WHEN s.Status='PUBLISHED' THEN 'PUBLISHED'
                        WHEN s.Status='FINALIZED' THEN 'FINALIZED'
                        WHEN scores.EvaluationCount < COALESCE(s.RequiredEvaluationCount,1) THEN 'INCOMPLETE'
                        ELSE 'READY' END AS EffectiveStatus
            FROM dbo.Interns i
            LEFT JOIN dbo.Users m ON m.Id=i.MentorUserId AND m.Role='MENTOR'
            LEFT JOIN dbo.InternshipPrograms p ON p.Id=i.InternshipProgramId
            LEFT JOIN dbo.EvaluationSummaries s ON s.InternId=i.Id
            OUTER APPLY (
                SELECT COUNT(*) EvaluationCount,CAST(ROUND(AVG(CAST(fe.FinalScore AS DECIMAL(10,4)))*10,2) AS DECIMAL(5,2)) AverageScore
                FROM dbo.FinalEvaluations fe WHERE fe.InternId=i.Id AND fe.EvaluationSource='MENTOR'
            ) scores
            OUTER APPLY (
                SELECT TOP(1) r.Label FROM dbo.EvaluationClassificationRules r
                WHERE r.IsActive=1 AND scores.AverageScore>=r.MinimumScore
                  AND (r.MaximumScore IS NULL OR scores.AverageScore<r.MaximumScore)
                ORDER BY r.SortOrder
            ) classificationRule
            {where}
            ORDER BY i.Id DESC
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
            """;

        var items = new List<EvaluationSummaryListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) items.Add(MapListItem(reader));
        return new PageResponse<EvaluationSummaryListItem>(items, current, pageSize, total, pages);
    }

    public async Task<EvaluationSummaryDetail?> GetDetailAsync(int internId, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        var header = await LoadHeaderAsync(connection, internId, cancellationToken);
        if (header is null) return null;

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT fe.Id,mentor.FullName,COALESCE(fe.Period,''),fe.Attendance,fe.Attitude,
                   fe.ProfessionalSkills,fe.WorkResult,fe.FinalScore,fe.Classification,
                   fe.Comment,fe.CreatedAt,fe.UpdatedAt
            FROM dbo.FinalEvaluations fe
            JOIN dbo.Users mentor ON mentor.Id=fe.CreatedByUserId
            WHERE fe.InternId=@internId AND fe.EvaluationSource='MENTOR'
            ORDER BY fe.CreatedAt DESC,fe.Id DESC;
            """;
        command.Parameters.Add("@internId", SqlDbType.Int).Value = internId;
        var evaluations = new List<MentorEvaluationItem>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                evaluations.Add(new MentorEvaluationItem(
                    reader.GetInt32(0), reader.GetString(1), reader.GetString(2),
                    NullableDecimal(reader, 3), NullableDecimal(reader, 4), NullableDecimal(reader, 5),
                    NullableDecimal(reader, 6), NullableDecimal(reader, 7), NullableString(reader, 8),
                    NullableString(reader, 9), reader.GetDateTimeOffset(10),
                    reader.IsDBNull(11) ? null : reader.GetDateTimeOffset(11)));
        }

        command.Parameters.Clear();
        command.CommandText = """
            SELECT s.Id,s.AggregateComment,s.FinalClassification,s.FinalizedByUserId,fu.FullName,s.FinalizedAt,
                   s.PublishedByUserId,pu.FullName,s.PublishedAt
            FROM dbo.EvaluationSummaries s
            LEFT JOIN dbo.Users fu ON fu.Id=s.FinalizedByUserId
            LEFT JOIN dbo.Users pu ON pu.Id=s.PublishedByUserId
            WHERE s.InternId=@internId;
            """;
        command.Parameters.Add("@internId", SqlDbType.Int).Value = internId;
        int? summaryId = null, finalizedBy = null, publishedBy = null;
        string? comment = null, classification = null, finalizedName = null, publishedName = null;
        DateTimeOffset? finalizedAt = null, publishedAt = null;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                summaryId = reader.GetInt32(0); comment = NullableString(reader, 1); classification = NullableString(reader, 2);
                finalizedBy = NullableInt(reader, 3); finalizedName = NullableString(reader, 4);
                finalizedAt = reader.IsDBNull(5) ? null : reader.GetDateTimeOffset(5);
                publishedBy = NullableInt(reader, 6); publishedName = NullableString(reader, 7);
                publishedAt = reader.IsDBNull(8) ? null : reader.GetDateTimeOffset(8);
            }
        }

        var history = summaryId.HasValue
            ? await LoadHistoryAsync(connection, summaryId.Value, cancellationToken)
            : [];
        return new EvaluationSummaryDetail(header, evaluations, comment, classification,
            finalizedBy, finalizedName, finalizedAt, publishedBy, publishedName, publishedAt, history);
    }

    public Task<bool> SaveAsync(int internId, int actorUserId, SaveEvaluationSummaryRequest request, CancellationToken ct) =>
        ChangeSummaryAsync(internId, actorUserId, request, "SAVE", ct);

    public Task<bool> FinalizeAsync(int internId, int actorUserId, SaveEvaluationSummaryRequest request, CancellationToken ct) =>
        ChangeSummaryAsync(internId, actorUserId, request, "FINALIZE", ct);

    public async Task<bool> PublishAsync(int internId, int actorUserId, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await using var command = new SqlCommand("""
            UPDATE dbo.EvaluationSummaries
            SET Status='PUBLISHED',PublishedByUserId=@actor,PublishedAt=SYSDATETIMEOFFSET(),UpdatedAt=SYSDATETIMEOFFSET()
            OUTPUT INSERTED.Id
            WHERE InternId=@internId AND Status='FINALIZED';
            """, connection, transaction);
        command.Parameters.Add("@internId", SqlDbType.Int).Value = internId;
        command.Parameters.Add("@actor", SqlDbType.Int).Value = actorUserId;
        var summaryId = await command.ExecuteScalarAsync(cancellationToken);
        if (summaryId is null) return false;
        command.CommandText = """
            INSERT dbo.EvaluationSummaryHistory(SummaryId,Action,ActorUserId,AggregateComment,FinalClassification)
            SELECT Id,'PUBLISHED',@actor,AggregateComment,FinalClassification
            FROM dbo.EvaluationSummaries WHERE Id=@summaryId;
            """;
        command.Parameters.Add("@summaryId", SqlDbType.Int).Value = Convert.ToInt32(summaryId);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<PublishedEvaluationResult?> GetPublishedForUserAsync(int userId, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("""
            SELECT i.Id,i.StudentCode,i.FullName,p.Name,
                   CAST(ROUND(AVG(CAST(fe.FinalScore AS DECIMAL(10,4)))*10,2) AS DECIMAL(5,2)),
                   s.FinalClassification,s.AggregateComment,s.PublishedAt
            FROM dbo.Users u
            JOIN dbo.Interns i ON i.Email=u.Email
            JOIN dbo.EvaluationSummaries s ON s.InternId=i.Id AND s.Status='PUBLISHED'
            LEFT JOIN dbo.InternshipPrograms p ON p.Id=i.InternshipProgramId
            JOIN dbo.FinalEvaluations fe ON fe.InternId=i.Id AND fe.EvaluationSource='MENTOR'
            WHERE u.Id=@userId AND u.Role='INTERN'
            GROUP BY i.Id,i.StudentCode,i.FullName,p.Name,s.FinalClassification,s.AggregateComment,s.PublishedAt;
            """, connection);
        command.Parameters.Add("@userId", SqlDbType.Int).Value = userId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new PublishedEvaluationResult(reader.GetInt32(0), NullableString(reader, 1), reader.GetString(2),
            NullableString(reader, 3), reader.GetDecimal(4), reader.GetString(5), reader.GetString(6), reader.GetDateTimeOffset(7));
    }

    public async Task<byte[]> ExportCsvAsync(
        string? search, int? programId, int? mentorId, string? status, CancellationToken cancellationToken)
    {
        var page = await GetPageAsync(1, 100, search, programId, mentorId, status, cancellationToken);
        var csv = new StringBuilder("Mã Intern,Họ tên,Email,Mentor,Chương trình,Số đánh giá,Số yêu cầu,Điểm trung bình,Xếp loại,Trạng thái\r\n");
        foreach (var item in page.Items)
        {
            csv.AppendLine(string.Join(',', new[] {
                Csv(item.StudentCode),Csv(item.FullName),Csv(item.Email),Csv(item.MentorName),Csv(item.ProgramName),
                item.EvaluationCount.ToString(CultureInfo.InvariantCulture),item.RequiredEvaluationCount.ToString(CultureInfo.InvariantCulture),
                item.AverageScore?.ToString("0.##",CultureInfo.InvariantCulture)??"",Csv(item.TemporaryClassification),Csv(item.StatusLabel)
            }));
        }
        return new UTF8Encoding(true).GetBytes(csv.ToString());
    }

    public async Task<IReadOnlyList<object>> GetFilterOptionsAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("""
            SELECT 'PROGRAM',Id,Name FROM dbo.InternshipPrograms WHERE Name IS NOT NULL
            UNION ALL SELECT 'MENTOR',Id,FullName FROM dbo.Users WHERE Role='MENTOR'
            ORDER BY 1,3;
            """, connection);
        var values = new List<object>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            values.Add(new { type = reader.GetString(0), id = reader.GetInt32(1), name = reader.GetString(2) });
        return values;
    }

    private async Task<bool> ChangeSummaryAsync(int internId, int actorUserId, SaveEvaluationSummaryRequest request, string operation, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await using var command = new SqlCommand("""
            IF NOT EXISTS(SELECT 1 FROM dbo.Interns WITH(UPDLOCK,HOLDLOCK) WHERE Id=@internId)
            BEGIN SELECT -1; RETURN; END;
            IF @operation='FINALIZE' AND
               (SELECT COUNT(*) FROM dbo.FinalEvaluations WHERE InternId=@internId AND EvaluationSource='MENTOR')
               < COALESCE((SELECT RequiredEvaluationCount FROM dbo.EvaluationSummaries WHERE InternId=@internId),1)
            BEGIN SELECT -2; RETURN; END;
            IF EXISTS(SELECT 1 FROM dbo.EvaluationSummaries WHERE InternId=@internId AND Status<>'DRAFT')
            BEGIN SELECT -3; RETURN; END;

            UPDATE dbo.EvaluationSummaries
            SET AggregateComment=@comment,FinalClassification=@classification,
                Status=CASE WHEN @operation='FINALIZE' THEN 'FINALIZED' ELSE 'DRAFT' END,
                FinalizedByUserId=CASE WHEN @operation='FINALIZE' THEN @actor ELSE NULL END,
                FinalizedAt=CASE WHEN @operation='FINALIZE' THEN SYSDATETIMEOFFSET() ELSE NULL END,
                UpdatedAt=SYSDATETIMEOFFSET()
            WHERE InternId=@internId;
            IF @@ROWCOUNT=0
                INSERT dbo.EvaluationSummaries
                    (InternId,AggregateComment,FinalClassification,Status,FinalizedByUserId,FinalizedAt)
                VALUES(@internId,@comment,@classification,
                    CASE WHEN @operation='FINALIZE' THEN 'FINALIZED' ELSE 'DRAFT' END,
                    CASE WHEN @operation='FINALIZE' THEN @actor ELSE NULL END,
                    CASE WHEN @operation='FINALIZE' THEN SYSDATETIMEOFFSET() ELSE NULL END);
            SELECT Id FROM dbo.EvaluationSummaries WHERE InternId=@internId;
            """, connection, transaction);
        command.Parameters.Add("@internId", SqlDbType.Int).Value = internId;
        command.Parameters.Add("@actor", SqlDbType.Int).Value = actorUserId;
        command.Parameters.Add("@operation", SqlDbType.VarChar, 10).Value = operation;
        command.Parameters.Add("@comment", SqlDbType.NVarChar, 2000).Value = request.AggregateComment.Trim();
        command.Parameters.Add("@classification", SqlDbType.NVarChar, 50).Value = request.FinalClassification.Trim();
        var result = Convert.ToInt32(await command.ExecuteScalarAsync(ct));
        if (result < 0) return false;
        command.CommandText = """
            INSERT dbo.EvaluationSummaryHistory(SummaryId,Action,ActorUserId,AggregateComment,FinalClassification)
            VALUES(@summaryId,CASE WHEN @operation='FINALIZE' THEN 'FINALIZED' ELSE 'SAVED' END,
                   @actor,@comment,@classification);
            """;
        command.Parameters.Add("@summaryId", SqlDbType.Int).Value = result;
        await command.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    private static string BuildWhere(string? search, int? programId, int? mentorId, string? status)
    {
        var where = new StringBuilder("WHERE 1=1");
        if (!string.IsNullOrWhiteSpace(search)) where.Append(" AND (i.FullName LIKE @search OR i.Email LIKE @search OR i.StudentCode LIKE @search)");
        if (programId.HasValue) where.Append(" AND i.InternshipProgramId=@programId");
        if (mentorId.HasValue) where.Append(" AND i.MentorUserId=@mentorId");
        if (!string.IsNullOrWhiteSpace(status)) where.Append(" AND CASE WHEN s.Status='PUBLISHED' THEN 'PUBLISHED' WHEN s.Status='FINALIZED' THEN 'FINALIZED' WHEN (SELECT COUNT(*) FROM dbo.FinalEvaluations f WHERE f.InternId=i.Id AND f.EvaluationSource='MENTOR') < COALESCE(s.RequiredEvaluationCount,1) THEN 'INCOMPLETE' ELSE 'READY' END=@status");
        return where.ToString();
    }

    private static void AddFilters(SqlCommand command, string? search, int? programId, int? mentorId, string? status)
    {
        if (!string.IsNullOrWhiteSpace(search)) command.Parameters.Add("@search", SqlDbType.NVarChar, 260).Value = $"%{search.Trim()}%";
        if (programId.HasValue) command.Parameters.Add("@programId", SqlDbType.Int).Value = programId.Value;
        if (mentorId.HasValue) command.Parameters.Add("@mentorId", SqlDbType.Int).Value = mentorId.Value;
        if (!string.IsNullOrWhiteSpace(status)) command.Parameters.Add("@status", SqlDbType.VarChar, 20).Value = status.Trim().ToUpperInvariant();
    }

    private static async Task<EvaluationSummaryListItem?> LoadHeaderAsync(SqlConnection connection, int internId, CancellationToken ct)
    {
        await using var command = new SqlCommand("""
            SELECT i.Id,i.StudentCode,i.FullName,i.Email,i.MentorUserId,m.FullName,i.InternshipProgramId,p.Name,
                   scores.EvaluationCount,COALESCE(s.RequiredEvaluationCount,1),scores.AverageScore,
                   COALESCE(classificationRule.Label,N'Chưa có'),
                   CASE WHEN s.Status='PUBLISHED' THEN 'PUBLISHED' WHEN s.Status='FINALIZED' THEN 'FINALIZED'
                        WHEN scores.EvaluationCount<COALESCE(s.RequiredEvaluationCount,1) THEN 'INCOMPLETE' ELSE 'READY' END
            FROM dbo.Interns i LEFT JOIN dbo.Users m ON m.Id=i.MentorUserId
            LEFT JOIN dbo.InternshipPrograms p ON p.Id=i.InternshipProgramId
            LEFT JOIN dbo.EvaluationSummaries s ON s.InternId=i.Id
            OUTER APPLY(SELECT COUNT(*) EvaluationCount,CAST(ROUND(AVG(CAST(FinalScore AS DECIMAL(10,4)))*10,2) AS DECIMAL(5,2)) AverageScore FROM dbo.FinalEvaluations WHERE InternId=i.Id AND EvaluationSource='MENTOR') scores
            OUTER APPLY(SELECT TOP(1) Label FROM dbo.EvaluationClassificationRules r WHERE r.IsActive=1 AND scores.AverageScore>=r.MinimumScore AND (r.MaximumScore IS NULL OR scores.AverageScore<r.MaximumScore) ORDER BY r.SortOrder) classificationRule
            WHERE i.Id=@internId;
            """, connection);
        command.Parameters.Add("@internId", SqlDbType.Int).Value = internId;
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapListItem(reader) : null;
    }

    private static async Task<IReadOnlyList<EvaluationSummaryHistoryItem>> LoadHistoryAsync(SqlConnection connection, int summaryId, CancellationToken ct)
    {
        await using var command = new SqlCommand("""
            SELECT h.Id,h.Action,u.FullName,h.AggregateComment,h.FinalClassification,h.OccurredAt
            FROM dbo.EvaluationSummaryHistory h JOIN dbo.Users u ON u.Id=h.ActorUserId
            WHERE h.SummaryId=@summaryId ORDER BY h.OccurredAt DESC,h.Id DESC;
            """, connection);
        command.Parameters.Add("@summaryId", SqlDbType.Int).Value = summaryId;
        var items = new List<EvaluationSummaryHistoryItem>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) items.Add(new(reader.GetInt64(0),reader.GetString(1),reader.GetString(2),NullableString(reader,3),NullableString(reader,4),reader.GetDateTimeOffset(5)));
        return items;
    }

    private static EvaluationSummaryListItem MapListItem(SqlDataReader reader)
    {
        var status = reader.GetString(12);
        return new(reader.GetInt32(0),NullableString(reader,1),reader.GetString(2),reader.GetString(3),NullableInt(reader,4),NullableString(reader,5),NullableInt(reader,6),NullableString(reader,7),reader.GetInt32(8),reader.GetInt32(9),NullableDecimal(reader,10),reader.GetString(11),status,StatusLabel(status));
    }

    private static string StatusLabel(string status) => status switch
    {
        "INCOMPLETE" => "Chưa đủ đánh giá", "READY" => "Chờ tổng hợp",
        "FINALIZED" => "Đã chốt", "PUBLISHED" => "Đã công bố", _ => status
    };
    private static string Csv(string? value) => $"\"{(value ?? "").Replace("\"", "\"\"")}\"";
    private static string? NullableString(SqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    private static int? NullableInt(SqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    private static decimal? NullableDecimal(SqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
}
