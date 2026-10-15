using System.Data;
using InternManagement.DTOs;
using Microsoft.Data.SqlClient;

namespace InternManagement.Services;

public sealed class WeeklyReportService(IConfiguration configuration)
{
    private readonly string connectionString = configuration.GetConnectionString("InternManagement")
        ?? throw new InvalidOperationException("Chưa cấu hình cơ sở dữ liệu.");

    // Current assignment is authoritative for Mentors; both account and profile own Intern reports.
    private const string Scope = """
        ((@mentor=1 AND i.MentorUserId=@user AND EXISTS(SELECT 1 FROM dbo.Users u WHERE u.Id=@user AND u.Role='MENTOR')) OR
         (@mentor=0 AND r.SubmittedByUserId=@user AND EXISTS(SELECT 1 FROM dbo.Users u WHERE u.Id=@user AND u.Role='INTERN' AND u.Email=i.Email)))
        """;

    public async Task<List<WeeklyReportResponse>> GetAsync(int userId, bool mentor, int? id, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT r.Id,r.InternId,i.FullName,r.InternshipProgramId,p.Name,r.WeekStartDate,r.Content,r.CreatedAt,r.WorkDone,r.Results,r.Difficulties,r.NextPlan
            FROM dbo.WeeklyReports r JOIN dbo.Interns i ON i.Id=r.InternId
            LEFT JOIN dbo.InternshipPrograms p ON p.Id=r.InternshipProgramId
            WHERE (@id IS NULL OR r.Id=@id) AND
            """ + Scope + " ORDER BY r.WeekStartDate DESC,r.Id DESC;";
        command.Parameters.Add("@user", SqlDbType.Int).Value = userId;
        command.Parameters.Add("@mentor", SqlDbType.Bit).Value = mentor;
        command.Parameters.Add("@id", SqlDbType.Int).Value = (object?)id ?? DBNull.Value;
        var reports = new List<WeeklyReportResponse>();
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var week = DateOnly.FromDateTime(reader.GetDateTime(5));
                reports.Add(new(reader.GetInt32(0),reader.GetInt32(1),reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetInt32(3),reader.IsDBNull(4) ? null : reader.GetString(4),
                    week,week.AddDays(6),reader.GetString(6),reader.GetFieldValue<DateTimeOffset>(7),[],
                    reader.IsDBNull(8)?null:reader.GetString(8),reader.IsDBNull(9)?null:reader.GetString(9),
                    reader.IsDBNull(10)?null:reader.GetString(10),reader.IsDBNull(11)?null:reader.GetString(11)));
            }
        }
        if (reports.Count == 0) return reports;
        command.CommandText = """
            SELECT f.ReportId,f.Id,f.MentorUserId,u.FullName,f.Content,f.CreatedAt
            FROM dbo.WeeklyReportFeedbacks f JOIN dbo.WeeklyReports r ON r.Id=f.ReportId
            JOIN dbo.Interns i ON i.Id=r.InternId JOIN dbo.Users u ON u.Id=f.MentorUserId
            WHERE (@id IS NULL OR r.Id=@id) AND
            """ + Scope + " ORDER BY f.Id;";
        var byId = reports.ToDictionary(r => r.Id);
        await using var feedback = await command.ExecuteReaderAsync(ct);
        while (await feedback.ReadAsync(ct))
            if (byId.TryGetValue(feedback.GetInt32(0),out var report))
                report.Feedback.Add(new(feedback.GetInt32(1),feedback.GetInt32(2),feedback.GetString(3),
                    feedback.GetString(4),feedback.GetFieldValue<DateTimeOffset>(5)));
        return reports;
    }

    public async Task<int?> CreateAsync(int userId, CreateWeeklyReportRequest request, CancellationToken ct)
    {
        var sections=new[]{request.WorkDone,request.Results,request.Difficulties,request.NextPlan};
        if(sections.Any(x=>x is not null)){
            if(sections.Any(string.IsNullOrWhiteSpace))throw new ArgumentException("Điền đủ bốn mục báo cáo.");
            request=request with { Content=$"Công việc đã làm:\n{request.WorkDone!.Trim()}\n\nKết quả:\n{request.Results!.Trim()}\n\nKhó khăn:\n{request.Difficulties!.Trim()}\n\nKế hoạch tiếp theo:\n{request.NextPlan!.Trim()}" };
        }
        if (request.WeekStartDate is null || string.IsNullOrWhiteSpace(request.Content) || request.Content.Length > 4000)
            throw new ArgumentException("Chọn tuần và nhập nội dung báo cáo (tối đa 4000 ký tự).");
        var day = request.WeekStartDate.Value;
        var week = day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
        if (week > DateOnly.MaxValue.AddDays(-6)) throw new ArgumentException("Tuần không hợp lệ.");
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);
        await using var command = new SqlCommand("""
            INSERT dbo.WeeklyReports(InternId,SubmittedByUserId,InternshipProgramId,WeekStartDate,Content,WorkDone,Results,Difficulties,NextPlan)
            OUTPUT INSERTED.Id
            SELECT i.Id,u.Id,i.InternshipProgramId,@week,@content,@work,@results,@difficulties,@plan
            FROM dbo.Interns i WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Users u ON u.Email=i.Email
            WHERE u.Id=@user AND u.Role='INTERN';
            """,connection,transaction);
        command.Parameters.Add("@user",SqlDbType.Int).Value = userId;
        command.Parameters.Add("@work",SqlDbType.NVarChar,4000).Value=(object?)request.WorkDone?.Trim()??DBNull.Value;
        command.Parameters.Add("@results",SqlDbType.NVarChar,4000).Value=(object?)request.Results?.Trim()??DBNull.Value;
        command.Parameters.Add("@difficulties",SqlDbType.NVarChar,4000).Value=(object?)request.Difficulties?.Trim()??DBNull.Value;
        command.Parameters.Add("@plan",SqlDbType.NVarChar,4000).Value=(object?)request.NextPlan?.Trim()??DBNull.Value;
        command.Parameters.Add("@week",SqlDbType.Date).Value = week.ToDateTime(TimeOnly.MinValue);
        command.Parameters.Add("@content",SqlDbType.NVarChar,4000).Value = request.Content.Trim();
        try
        {
            var id = await command.ExecuteScalarAsync(ct);
            await transaction.CommitAsync(ct);
            return id is null ? null : Convert.ToInt32(id);
        }
        catch (SqlException error) when (error.Number is 2601 or 2627)
        {
            throw new InvalidOperationException("Tuần này đã có báo cáo. Báo cáo đã gửi được giữ trong lịch sử.");
        }
    }

    public async Task<int?> AddFeedbackAsync(int userId, int reportId, CreateWeeklyReportFeedbackRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Content) || request.Content.Length > 2000)
            throw new ArgumentException("Nhập phản hồi (tối đa 2000 ký tự).");
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);
        await using var command = new SqlCommand("""
            INSERT dbo.WeeklyReportFeedbacks(ReportId,MentorUserId,Content)
            OUTPUT INSERTED.Id
            SELECT r.Id,@user,@content FROM dbo.WeeklyReports r JOIN dbo.Interns i WITH(UPDLOCK,HOLDLOCK) ON i.Id=r.InternId
            WHERE r.Id=@report AND i.MentorUserId=@user
                AND EXISTS(SELECT 1 FROM dbo.Users u WHERE u.Id=@user AND u.Role='MENTOR');
            """,connection,transaction);
        command.Parameters.Add("@user",SqlDbType.Int).Value = userId;
        command.Parameters.Add("@report",SqlDbType.Int).Value = reportId;
        command.Parameters.Add("@content",SqlDbType.NVarChar,2000).Value = request.Content.Trim();
        var id = await command.ExecuteScalarAsync(ct);
        if (id is not null) {
            command.CommandText = """
                INSERT dbo.Notifications(UserId,Title,Message,Category,Type,ActionUrl,CreatedBy)
                SELECT r.SubmittedByUserId,N'Phản hồi báo cáo tuần',LEFT(@content,1000),'SYSTEM','REPORT_FEEDBACK','weekly-reports.html',@user
                FROM dbo.WeeklyReports r WHERE r.Id=@report;
                """;
            await command.ExecuteNonQueryAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return id is null ? null : Convert.ToInt32(id);
    }
}
