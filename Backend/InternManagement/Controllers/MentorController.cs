using System.Data;
using InternManagement.DTOs;
using InternManagement.Models;
using InternManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace InternManagement.Controllers;

[ApiController]
[Route("api/mentor")]
public sealed class MentorController : ControllerBase
{
    private readonly string connectionString;
    private readonly AuthTokenService tokens;

    public MentorController(IConfiguration configuration, AuthTokenService tokens)
    {
        connectionString = configuration.GetConnectionString("InternManagement")
            ?? throw new InvalidOperationException("ConnectionStrings:InternManagement is missing.");
        this.tokens = tokens;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        if (!TryMentor(out var mentor, out var error)) return error!;
        await using var db = new SqlConnection(connectionString);
        await db.OpenAsync(ct);
        await using var stats = db.CreateCommand();
        stats.CommandText = """
            SELECT
              (SELECT COUNT(*) FROM dbo.MentorInternAssignments WHERE MentorUserId=@mentor) AS Interns,
              (SELECT COUNT(*) FROM dbo.MentorInternAssignments a JOIN dbo.Interns i ON i.Id=a.InternId WHERE a.MentorUserId=@mentor AND i.Status=N'Đang thực tập') AS ActiveInterns,
              (SELECT COUNT(*) FROM dbo.InternTasks t JOIN dbo.MentorInternAssignments a ON a.InternId=t.InternId WHERE a.MentorUserId=@mentor AND t.MentorUserId=@mentor AND t.Status IN (N'NotStarted',N'InProgress',N'PendingReview')) AS OpenTasks,
              (SELECT COUNT(*) FROM dbo.InternTasks t JOIN dbo.MentorInternAssignments a ON a.InternId=t.InternId WHERE a.MentorUserId=@mentor AND t.MentorUserId=@mentor AND t.Status=N'Completed') AS CompletedTasks,
              (SELECT COUNT(*) FROM dbo.InternReports r JOIN dbo.MentorInternAssignments a ON a.InternId=r.InternId WHERE a.MentorUserId=@mentor AND r.Status IN (N'Submitted',N'InReview')) AS PendingReports,
              (SELECT COUNT(*) FROM dbo.UserNotifications WHERE UserId=@mentor AND ReadAt IS NULL) AS UnreadNotifications,
              (SELECT COUNT(*) FROM dbo.InternFeedback f JOIN dbo.MentorInternAssignments a ON a.InternId=f.InternId WHERE a.MentorUserId=@mentor AND f.MentorUserId=@mentor AND f.CreatedAt>DATEADD(DAY,-7,SYSUTCDATETIME())) AS RecentFeedback;
            """;
        stats.Parameters.AddWithValue("@mentor", mentor!.Id);
        await using var reader = await stats.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        var counts = new
        {
            assignedInterns = reader.GetInt32(0), activeInterns = reader.GetInt32(1),
            pendingTasks = reader.GetInt32(2), completedTasks = reader.GetInt32(3),
            reportsToReview = reader.GetInt32(4), unreadNotifications = reader.GetInt32(5),
            recentFeedback = reader.GetInt32(6)
        };
        await reader.CloseAsync();

        var interns = await ReadRows(db, """
            SELECT TOP (5) i.Id, i.FullName AS Name, i.Mssv, i.Email, i.Status, i.Progress, i.InternshipPosition AS Position
            FROM dbo.MentorInternAssignments a JOIN dbo.Interns i ON i.Id=a.InternId
            WHERE a.MentorUserId=@mentor ORDER BY a.AssignedAt DESC;
            """, mentor.Id, ct);
        var tasks = await ReadRows(db, """
            SELECT TOP (5) t.Id, t.Title, t.Status, t.Deadline, i.FullName AS InternName
            FROM dbo.InternTasks t JOIN dbo.MentorInternAssignments a ON a.InternId=t.InternId
            JOIN dbo.Interns i ON i.Id=t.InternId
            WHERE a.MentorUserId=@mentor AND t.MentorUserId=@mentor AND t.Status<>N'Completed'
            ORDER BY CASE WHEN t.Deadline IS NULL THEN 1 ELSE 0 END, t.Deadline;
            """, mentor.Id, ct);
        var reports = await ReadRows(db, """
            SELECT TOP (5) r.Id, r.Title, r.Status, r.SubmittedAt, i.FullName AS InternName
            FROM dbo.InternReports r JOIN dbo.MentorInternAssignments a ON a.InternId=r.InternId
            JOIN dbo.Interns i ON i.Id=r.InternId
            WHERE a.MentorUserId=@mentor AND r.Status IN (N'Submitted',N'InReview') ORDER BY r.SubmittedAt DESC;
            """, mentor.Id, ct);
        var schedules = await ReadRows(db, """
            SELECT TOP (5) s.Id, s.Title, s.StartsAt, s.EndsAt, s.Location, i.FullName AS InternName
            FROM dbo.MentoringSchedules s JOIN dbo.MentorInternAssignments a ON a.InternId=s.InternId
            JOIN dbo.Interns i ON i.Id=s.InternId
            WHERE s.MentorUserId=@mentor AND a.MentorUserId=@mentor AND s.Status=N'Scheduled' AND s.StartsAt>=SYSUTCDATETIME()
            ORDER BY s.StartsAt;
            """, mentor.Id, ct);
        return Ok(new { mentor = await GetProfileData(db, mentor.Id, ct), stats = counts, recentInterns = interns, pendingTasks = tasks, recentReports = reports, upcomingSchedules = schedules });
    }

    [HttpGet("interns")]
    public async Task<IActionResult> GetInterns([FromQuery] string? search, [FromQuery] string? status,
        [FromQuery] string? progress, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        if (!TryMentor(out var mentor, out var error)) return error!;
        page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize, 1, 1000);
        await using var db = new SqlConnection(connectionString); await db.OpenAsync(ct);
        await using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT i.Id, i.FullName AS Name, i.Mssv, i.Email, i.Phone, i.InternshipPosition AS Position,
                   i.Department, i.StartDate, i.EndDate, i.Status, i.Progress, i.Mentor,
                   COUNT(t.Id) AS TaskCount, SUM(CASE WHEN t.Status=N'Completed' THEN 1 ELSE 0 END) AS DoneTasks
            FROM dbo.MentorInternAssignments a JOIN dbo.Interns i ON i.Id=a.InternId
            LEFT JOIN dbo.InternTasks t ON t.InternId=i.Id AND t.MentorUserId=a.MentorUserId
            WHERE a.MentorUserId=@mentor
              AND (@search IS NULL OR i.FullName LIKE @like OR i.Mssv LIKE @like OR i.Email LIKE @like)
              AND (@status IS NULL OR i.Status=@status)
              AND (@progress IS NULL OR (@progress=N'Low' AND i.Progress<40) OR (@progress=N'Medium' AND i.Progress BETWEEN 40 AND 79) OR (@progress=N'High' AND i.Progress>=80))
            GROUP BY i.Id,i.FullName,i.Mssv,i.Email,i.Phone,i.InternshipPosition,i.Department,i.StartDate,i.EndDate,i.Status,i.Progress,i.Mentor,a.AssignedAt
            ORDER BY a.AssignedAt DESC OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
            """;
        cmd.Parameters.AddWithValue("@mentor", mentor!.Id);
        cmd.Parameters.AddWithValue("@search", Db(search)); cmd.Parameters.AddWithValue("@like", $"%{search?.Trim()}%");
        cmd.Parameters.AddWithValue("@status", Db(status)); cmd.Parameters.AddWithValue("@progress", Db(progress));
        cmd.Parameters.AddWithValue("@offset", (page - 1) * pageSize); cmd.Parameters.AddWithValue("@pageSize", pageSize);
        var rows = await ReadRows(cmd, ct);
        await using var count = db.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM dbo.MentorInternAssignments a JOIN dbo.Interns i ON i.Id=a.InternId WHERE a.MentorUserId=@mentor AND (@search IS NULL OR i.FullName LIKE @like OR i.Mssv LIKE @like OR i.Email LIKE @like) AND (@status IS NULL OR i.Status=@status)";
        count.Parameters.AddWithValue("@mentor", mentor.Id); count.Parameters.AddWithValue("@search", Db(search)); count.Parameters.AddWithValue("@like", $"%{search?.Trim()}%"); count.Parameters.AddWithValue("@status", Db(status));
        var total = Convert.ToInt32(await count.ExecuteScalarAsync(ct));
        return Ok(new { items = rows, total, page, pageSize });
    }

    [HttpGet("interns/{id:int}")]
    public async Task<IActionResult> GetIntern(int id, CancellationToken ct)
    {
        if (!TryMentor(out var mentor, out var error)) return error!;
        await using var db = new SqlConnection(connectionString); await db.OpenAsync(ct);
        await using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT i.Id,i.FullName AS Name,i.Mssv,i.Email,i.Phone,i.School,i.Major,i.InternshipPosition AS Position,
              i.Department,i.StartDate,i.EndDate,i.Status,i.Progress,a.MentorUserId
            FROM dbo.Interns i JOIN dbo.MentorInternAssignments a ON a.InternId=i.Id WHERE i.Id=@id;
            """;
        cmd.Parameters.AddWithValue("@id", id);
        var item = await ReadRows(cmd, ct);
        if (item.Count == 0) return NotFound(new { message = "Không tìm thấy thực tập sinh được phân công." });
        if (Convert.ToInt32(item[0]["MentorUserId"]) != mentor!.Id) return NotFound(new { message = "Không tìm thấy thực tập sinh được phân công." });
        var tasks = await ReadRows(db, "SELECT Id,Title,Description,AssignedAt,Deadline,Priority,Status FROM dbo.InternTasks WHERE InternId=@intern AND MentorUserId=@mentor ORDER BY AssignedAt DESC", mentor.Id, ct, ("@intern", id));
        var reports = await ReadRows(db, "SELECT Id,Title,Status,SubmittedAt FROM dbo.InternReports WHERE InternId=@intern ORDER BY SubmittedAt DESC", mentor.Id, ct, ("@intern", id));
        var feedback = await ReadRows(db, "SELECT Id,Content,Type,Severity,CreatedAt FROM dbo.InternFeedback WHERE InternId=@intern AND MentorUserId=@mentor ORDER BY CreatedAt DESC", mentor.Id, ct, ("@intern", id));
        var schedule = await ReadRows(db, "SELECT Id,Title,Content,StartsAt,EndsAt,Location,Notes,Status FROM dbo.MentoringSchedules WHERE InternId=@intern AND MentorUserId=@mentor ORDER BY StartsAt DESC", mentor.Id, ct, ("@intern", id));
        return Ok(new { intern = item[0], tasks, reports, feedback, schedule });
    }

    [HttpGet("tasks")]
    public async Task<IActionResult> GetTasks([FromQuery] string? status, [FromQuery] int? internId, CancellationToken ct)
    {
        if (!TryMentor(out var mentor, out var error)) return error!;
        await using var db = new SqlConnection(connectionString); await db.OpenAsync(ct);
        await using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT t.Id,t.InternId,t.Title,t.Description,t.AssignedAt,t.Deadline,t.Priority,t.Status,i.FullName AS InternName,i.Mssv
            FROM dbo.InternTasks t JOIN dbo.MentorInternAssignments a ON a.InternId=t.InternId JOIN dbo.Interns i ON i.Id=t.InternId
            WHERE a.MentorUserId=@mentor AND t.MentorUserId=@mentor AND (@status IS NULL OR t.Status=@status) AND (@internId IS NULL OR t.InternId=@internId)
            ORDER BY CASE WHEN t.Deadline IS NULL THEN 1 ELSE 0 END,t.Deadline,t.AssignedAt DESC;
            """;
        cmd.Parameters.AddWithValue("@mentor", mentor!.Id); cmd.Parameters.AddWithValue("@status", Db(status)); cmd.Parameters.AddWithValue("@internId", (object?)internId ?? DBNull.Value);
        return Ok(await ReadRows(cmd, ct));
    }

    [HttpPost("tasks")]
    public async Task<IActionResult> CreateTask(MentorTaskRequest request, CancellationToken ct)
    {
        if (!TryMentor(out var mentor, out var error)) return error!;
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 200 || !ValidPriority(request.Priority))
            return BadRequest(new { message = "Tiêu đề hoặc mức ưu tiên không hợp lệ." });
        await using var db = new SqlConnection(connectionString); await db.OpenAsync(ct);
        if (!await IsAssigned(db, mentor!.Id, request.InternId, ct)) return Forbid();
        await using var cmd = db.CreateCommand();
        cmd.CommandText = "INSERT dbo.InternTasks(InternId,MentorUserId,Title,Description,Deadline,Priority) OUTPUT inserted.Id VALUES(@intern,@mentor,@title,@description,@deadline,@priority)";
        AddTaskParameters(cmd, request, mentor.Id);
        var id = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        await NotifyIntern(db, request.InternId, "TaskAssigned", "Có nhiệm vụ mới", request.Title!.Trim(), ct);
        return Created($"/api/mentor/tasks/{id}", new { id, message = "Đã giao nhiệm vụ." });
    }

    [HttpPost("tasks/group")]
    public async Task<IActionResult> CreateGroupTask(MentorGroupTaskRequest request, CancellationToken ct)
    {
        if (!TryMentor(out var mentor, out var error)) return error!;
        var internIds = request.InternIds?.Distinct().ToArray() ?? [];
        if (internIds.Length == 0 || internIds.Length > 1000 || string.IsNullOrWhiteSpace(request.Title)
            || request.Title.Trim().Length > 200 || !ValidPriority(request.Priority) || request.Description?.Length > 2000)
            return BadRequest(new { message = "Nhóm thực tập sinh, tiêu đề hoặc mức ưu tiên không hợp lệ." });

        await using var db = new SqlConnection(connectionString); await db.OpenAsync(ct);
        foreach (var internId in internIds)
            if (!await IsAssigned(db, mentor!.Id, internId, ct))
                return Forbid();

        await using var tx = (SqlTransaction)await db.BeginTransactionAsync(ct);
        try
        {
            foreach (var internId in internIds)
            {
                await using var task = db.CreateCommand(); task.Transaction = tx;
                task.CommandText = "INSERT dbo.InternTasks(InternId,MentorUserId,Title,Description,Deadline,Priority) VALUES(@intern,@mentor,@title,@description,@deadline,@priority)";
                task.Parameters.AddWithValue("@intern", internId); task.Parameters.AddWithValue("@mentor", mentor!.Id);
                task.Parameters.AddWithValue("@title", request.Title!.Trim()); task.Parameters.AddWithValue("@description", Db(request.Description));
                task.Parameters.AddWithValue("@deadline", (object?)request.Deadline?.Date ?? DBNull.Value); task.Parameters.AddWithValue("@priority", request.Priority!);
                await task.ExecuteNonQueryAsync(ct);
                await NotifyIntern(db, internId, "TaskAssigned", "Có nhiệm vụ mới", request.Title.Trim(), ct, tx);
            }
            await tx.CommitAsync(ct);
            return Ok(new { assignedCount = internIds.Length, message = $"Đã giao nhiệm vụ cho {internIds.Length} thực tập sinh." });
        }
        catch { await tx.RollbackAsync(ct); throw; }
    }

    [HttpPut("tasks/{id:int}")]
    public async Task<IActionResult> UpdateTask(int id, MentorTaskRequest request, CancellationToken ct)
    {
        if (!TryMentor(out var mentor, out var error)) return error!;
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 200 || !ValidPriority(request.Priority)) return BadRequest(new { message = "Tiêu đề hoặc mức ưu tiên không hợp lệ." });
        await using var db = new SqlConnection(connectionString); await db.OpenAsync(ct);
        if (!await IsAssigned(db, mentor!.Id, request.InternId, ct)) return Forbid();
        await using var cmd = db.CreateCommand();
        cmd.CommandText = "UPDATE t SET Title=@title,Description=@description,Deadline=@deadline,Priority=@priority FROM dbo.InternTasks t JOIN dbo.MentorInternAssignments a ON a.InternId=t.InternId WHERE t.Id=@id AND t.InternId=@intern AND t.MentorUserId=@mentor AND a.MentorUserId=@mentor";
        AddTaskParameters(cmd, request, mentor.Id); cmd.Parameters.AddWithValue("@id", id);
        var updated = await cmd.ExecuteNonQueryAsync(ct);
        return updated == 0 ? NotFound(new { message = "Không tìm thấy nhiệm vụ." }) : Ok(new { message = "Đã cập nhật nhiệm vụ." });
    }

    [HttpDelete("tasks/{id:int}")]
    public async Task<IActionResult> DeleteTask(int id, CancellationToken ct)
    {
        if (!TryMentor(out var mentor, out var error)) return error!;
        await using var db = new SqlConnection(connectionString); await db.OpenAsync(ct);
        await using var cmd = db.CreateCommand(); cmd.CommandText = "DELETE t FROM dbo.InternTasks t JOIN dbo.MentorInternAssignments a ON a.InternId=t.InternId WHERE t.Id=@id AND t.MentorUserId=@mentor AND a.MentorUserId=@mentor AND t.Status=N'NotStarted'";
        cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@mentor", mentor!.Id);
        return await cmd.ExecuteNonQueryAsync(ct) == 0 ? NotFound(new { message = "Không tìm thấy nhiệm vụ có thể xóa." }) : NoContent();
    }

    [HttpPut("tasks/{id:int}/review")]
    public async Task<IActionResult> ReviewTask(int id, TaskReviewRequest request, CancellationToken ct)
    {
        if (!TryMentor(out var mentor, out var error)) return error!;
        if (request.Status is not ("Completed" or "InProgress") || request.Feedback?.Length > 2000)
            return BadRequest(new { message = "Tráº¡ng thÃ¡i hoáº·c nháº­n xÃ©t khÃ´ng há»£p lá»‡." });
        await using var db = new SqlConnection(connectionString); await db.OpenAsync(ct);
        await using var cmd = db.CreateCommand();
        cmd.CommandText = "UPDATE t SET Status=@status OUTPUT inserted.InternId FROM dbo.InternTasks t JOIN dbo.MentorInternAssignments a ON a.InternId=t.InternId WHERE t.Id=@id AND t.MentorUserId=@mentor AND a.MentorUserId=@mentor AND t.Status=N'PendingReview'";
        cmd.Parameters.AddWithValue("@status", request.Status); cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@mentor", mentor!.Id);
        var result = await cmd.ExecuteScalarAsync(ct);
        if (result is null) return NotFound(new { message = "KhÃ´ng tÃ¬m tháº¥y nhiá»‡m vá»¥ Ä‘ang chá» Ä‘Ã¡nh giÃ¡." });
        if (!string.IsNullOrWhiteSpace(request.Feedback))
            await NotifyIntern(db, Convert.ToInt32(result), "TaskReviewed", request.Status == "Completed" ? "Nhiá»‡m vá»¥ Ä‘Ã£ hoÃ n thÃ nh" : "Mentor yÃªu cáº§u tiáº¿p tá»¥c", request.Feedback.Trim(), ct);
        return Ok(new { message = "ÄÃ£ ghi nháº­n Ä‘Ã¡nh giÃ¡ nhiá»‡m vá»¥." });
    }

    [HttpGet("reports")]
    public async Task<IActionResult> GetReports([FromQuery] string? status, CancellationToken ct)
    {
        if (!TryMentor(out var mentor, out var error)) return error!;
        await using var db = new SqlConnection(connectionString); await db.OpenAsync(ct);
        await using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT r.Id,r.InternId,r.Title,r.Content,r.AttachmentUrl,r.Status,r.SubmittedAt,i.FullName AS InternName,i.Mssv FROM dbo.InternReports r JOIN dbo.MentorInternAssignments a ON a.InternId=r.InternId JOIN dbo.Interns i ON i.Id=r.InternId WHERE a.MentorUserId=@mentor AND (@status IS NULL OR r.Status=@status) ORDER BY r.SubmittedAt DESC";
        cmd.Parameters.AddWithValue("@mentor", mentor!.Id); cmd.Parameters.AddWithValue("@status", Db(status));
        return Ok(await ReadRows(cmd, ct));
    }

    [HttpGet("reports/{id:int}")]
    public async Task<IActionResult> GetReport(int id, CancellationToken ct)
    {
        if (!TryMentor(out var mentor, out var error)) return error!;
        await using var db = new SqlConnection(connectionString); await db.OpenAsync(ct);
        var report = await ReadRows(db, "SELECT r.Id,r.InternId,r.Title,r.Content,r.AttachmentUrl,r.Status,r.SubmittedAt,i.FullName AS InternName FROM dbo.InternReports r JOIN dbo.MentorInternAssignments a ON a.InternId=r.InternId JOIN dbo.Interns i ON i.Id=r.InternId WHERE r.Id=@id AND a.MentorUserId=@mentor", mentor!.Id, ct, ("@id", id));
        if (report.Count == 0) return NotFound(new { message = "Không tìm thấy báo cáo được phân công." });
        var history = await ReadRows(db, "SELECT Id,Status,Feedback,Score,ReviewedAt FROM dbo.ReportReviews WHERE ReportId=@id ORDER BY ReviewedAt DESC", mentor.Id, ct, ("@id", id));
        return Ok(new { report = report[0], reviewHistory = history });
    }

    [HttpPut("reports/{id:int}/review")]
    public async Task<IActionResult> ReviewReport(int id, ReportReviewRequest request, CancellationToken ct)
    {
        if (!TryMentor(out var mentor, out var error)) return error!;
        var allowed = new[] { "RevisionRequested", "Approved", "Rejected" };
        if (request.Status is null || !allowed.Contains(request.Status) || string.IsNullOrWhiteSpace(request.Feedback) || request.Feedback.Length > 2000 || (request.Score is < 0 or > 10))
            return BadRequest(new { message = "Trạng thái, lý do nhận xét hoặc điểm không hợp lệ." });
        await using var db = new SqlConnection(connectionString); await db.OpenAsync(ct);
        await using var tx = (SqlTransaction)await db.BeginTransactionAsync(ct);
        try
        {
            await using var review = db.CreateCommand(); review.Transaction = tx;
            review.CommandText = "INSERT dbo.ReportReviews(ReportId,MentorUserId,Status,Feedback,Score) SELECT r.Id,@mentor,@status,@feedback,@score FROM dbo.InternReports r JOIN dbo.MentorInternAssignments a ON a.InternId=r.InternId WHERE r.Id=@id AND a.MentorUserId=@mentor AND r.Status IN (N'Submitted',N'InReview',N'RevisionRequested')";
            review.Parameters.AddWithValue("@mentor", mentor!.Id); review.Parameters.AddWithValue("@status", request.Status); review.Parameters.AddWithValue("@feedback", request.Feedback.Trim()); review.Parameters.AddWithValue("@score", (object?)request.Score ?? DBNull.Value); review.Parameters.AddWithValue("@id", id);
            if (await review.ExecuteNonQueryAsync(ct) == 0) { await tx.RollbackAsync(ct); return NotFound(new { message = "Không tìm thấy báo cáo cần đánh giá." }); }
            await using var update = db.CreateCommand(); update.Transaction = tx;
            update.CommandText = "UPDATE dbo.InternReports SET Status=@status WHERE Id=@id";
            update.Parameters.AddWithValue("@status", request.Status); update.Parameters.AddWithValue("@id", id); await update.ExecuteNonQueryAsync(ct);
            await using var notice = db.CreateCommand(); notice.Transaction = tx;
            notice.CommandText = "INSERT dbo.UserNotifications(UserId,Type,Title,Message) SELECT i.UserId,N'ReportReviewed',N'Báo cáo đã được nhận xét',@message FROM dbo.InternReports r JOIN dbo.Interns i ON i.Id=r.InternId WHERE r.Id=@id AND i.UserId IS NOT NULL";
            notice.Parameters.AddWithValue("@message", request.Feedback.Trim()); notice.Parameters.AddWithValue("@id", id); await notice.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct); return Ok(new { message = "Đã lưu nhận xét báo cáo." });
        }
        catch { await tx.RollbackAsync(ct); throw; }
    }

    [HttpGet("evaluations")]
    public async Task<IActionResult> GetEvaluations([FromQuery] int? internId, CancellationToken ct)
    {
        if (!TryMentor(out var mentor, out var error)) return error!;
        await using var db = new SqlConnection(connectionString); await db.OpenAsync(ct);
        await using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT e.Id,e.InternId,e.Comments,e.Recommendation,e.IsSubmitted,e.UpdatedAt,i.FullName AS InternName FROM dbo.InternEvaluations e JOIN dbo.MentorInternAssignments a ON a.InternId=e.InternId JOIN dbo.Interns i ON i.Id=e.InternId WHERE e.MentorUserId=@mentor AND a.MentorUserId=@mentor AND (@internId IS NULL OR e.InternId=@internId) ORDER BY e.UpdatedAt DESC";
        cmd.Parameters.AddWithValue("@mentor", mentor!.Id); cmd.Parameters.AddWithValue("@internId", (object?)internId ?? DBNull.Value);
        var evaluations = await ReadRows(cmd, ct);
        foreach (var row in evaluations)
            row["scores"] = await ReadRows(db, "SELECT Criterion,Score FROM dbo.InternEvaluationScores WHERE EvaluationId=@id", mentor.Id, ct, ("@id", row["Id"]));
        return Ok(evaluations);
    }

    [HttpPut("evaluations/{internId:int}")]
    public async Task<IActionResult> SaveEvaluation(int internId, EvaluationRequest request, CancellationToken ct)
    {
        if (!TryMentor(out var mentor, out var error)) return error!;
        if (request.Scores is null || request.Scores.Count == 0 || request.Scores.Any(p => string.IsNullOrWhiteSpace(p.Key) || p.Key.Length > 100 || p.Value is < 0 or > 10))
            return BadRequest(new { message = "Cần nhập tiêu chí và điểm hợp lệ từ 0 đến 10." });
        await using var db = new SqlConnection(connectionString); await db.OpenAsync(ct);
        if (!await IsAssigned(db, mentor!.Id, internId, ct)) return NotFound(new { message = "Không tìm thấy thực tập sinh được phân công." });
        await using var tx = (SqlTransaction)await db.BeginTransactionAsync(ct);
        try
        {
            await using var save = db.CreateCommand(); save.Transaction = tx;
            save.CommandText = "IF EXISTS(SELECT 1 FROM dbo.InternEvaluations WHERE InternId=@intern AND MentorUserId=@mentor) UPDATE dbo.InternEvaluations SET Comments=@comments,Recommendation=@recommendation,IsSubmitted=@submitted,UpdatedAt=SYSUTCDATETIME() WHERE InternId=@intern AND MentorUserId=@mentor ELSE INSERT dbo.InternEvaluations(InternId,MentorUserId,Comments,Recommendation,IsSubmitted) VALUES(@intern,@mentor,@comments,@recommendation,@submitted); SELECT Id FROM dbo.InternEvaluations WHERE InternId=@intern AND MentorUserId=@mentor;";
            save.Parameters.AddWithValue("@intern", internId); save.Parameters.AddWithValue("@mentor", mentor.Id); save.Parameters.AddWithValue("@comments", Db(request.Comments)); save.Parameters.AddWithValue("@recommendation", Db(request.Recommendation)); save.Parameters.AddWithValue("@submitted", request.Submit);
            var evaluationId = Convert.ToInt32(await save.ExecuteScalarAsync(ct));
            await using var clear = db.CreateCommand(); clear.Transaction = tx; clear.CommandText = "DELETE FROM dbo.InternEvaluationScores WHERE EvaluationId=@id"; clear.Parameters.AddWithValue("@id", evaluationId); await clear.ExecuteNonQueryAsync(ct);
            foreach (var pair in request.Scores)
            {
                await using var score = db.CreateCommand(); score.Transaction = tx; score.CommandText = "INSERT dbo.InternEvaluationScores(EvaluationId,Criterion,Score) VALUES(@id,@criterion,@score)";
                score.Parameters.AddWithValue("@id", evaluationId); score.Parameters.AddWithValue("@criterion", pair.Key.Trim()); score.Parameters.AddWithValue("@score", pair.Value); await score.ExecuteNonQueryAsync(ct);
            }
            if (request.Submit) await NotifyIntern(db, internId, "EvaluationSubmitted", "Mentor đã gửi đánh giá", "Bạn có đánh giá mới từ Mentor.", ct, tx);
            await tx.CommitAsync(ct); return Ok(new { id = evaluationId, message = request.Submit ? "Đã gửi đánh giá." : "Đã lưu bản nháp đánh giá." });
        }
        catch { await tx.RollbackAsync(ct); throw; }
    }

    [HttpGet("feedback")]
    public async Task<IActionResult> GetFeedback([FromQuery] int? internId, CancellationToken ct)
    {
        if (!TryMentor(out var mentor, out var error)) return error!;
        await using var db = new SqlConnection(connectionString); await db.OpenAsync(ct);
        return Ok(await ReadRows(db, "SELECT f.Id,f.InternId,f.Content,f.Type,f.Severity,f.CreatedAt,i.FullName AS InternName FROM dbo.InternFeedback f JOIN dbo.MentorInternAssignments a ON a.InternId=f.InternId JOIN dbo.Interns i ON i.Id=f.InternId WHERE f.MentorUserId=@mentor AND a.MentorUserId=@mentor AND (@internId IS NULL OR f.InternId=@internId) ORDER BY f.CreatedAt DESC", mentor!.Id, ct, ("@internId", (object?)internId ?? DBNull.Value)));
    }

    [HttpPost("feedback")]
    public async Task<IActionResult> CreateFeedback(MentorFeedbackRequest request, CancellationToken ct)
    {
        if (!TryMentor(out var mentor, out var error)) return error!;
        var types = new[] { "Praise", "Suggestion", "Warning", "Improvement" }; var severities = new[] { "Low", "Normal", "High" };
        if (string.IsNullOrWhiteSpace(request.Content) || request.Content.Length > 2000 || request.Type is null || !types.Contains(request.Type) || request.Severity is null || !severities.Contains(request.Severity)) return BadRequest(new { message = "Nội dung, loại hoặc mức độ feedback không hợp lệ." });
        await using var db = new SqlConnection(connectionString); await db.OpenAsync(ct);
        if (!await IsAssigned(db, mentor!.Id, request.InternId, ct)) return Forbid();
        await using var cmd = db.CreateCommand(); cmd.CommandText = "INSERT dbo.InternFeedback(InternId,MentorUserId,Content,Type,Severity) VALUES(@intern,@mentor,@content,@type,@severity)";
        cmd.Parameters.AddWithValue("@intern", request.InternId); cmd.Parameters.AddWithValue("@mentor", mentor.Id); cmd.Parameters.AddWithValue("@content", request.Content.Trim()); cmd.Parameters.AddWithValue("@type", request.Type); cmd.Parameters.AddWithValue("@severity", request.Severity); await cmd.ExecuteNonQueryAsync(ct);
        await NotifyIntern(db, request.InternId, "MentorFeedback", "Mentor đã gửi phản hồi", request.Content.Trim(), ct);
        return Ok(new { message = "Đã gửi feedback cho thực tập sinh." });
    }

    [HttpGet("schedules")]
    public async Task<IActionResult> GetSchedules([FromQuery] bool upcoming = false, CancellationToken ct = default)
    {
        if (!TryMentor(out var mentor, out var error)) return error!;
        await using var db = new SqlConnection(connectionString); await db.OpenAsync(ct);
        var filter = upcoming ? "AND s.StartsAt>=SYSUTCDATETIME() AND s.Status=N'Scheduled'" : "";
        return Ok(await ReadRows(db, $"SELECT s.Id,s.InternId,s.Title,s.Content,s.StartsAt,s.EndsAt,s.Location,s.Notes,s.Status,i.FullName AS InternName FROM dbo.MentoringSchedules s JOIN dbo.MentorInternAssignments a ON a.InternId=s.InternId JOIN dbo.Interns i ON i.Id=s.InternId WHERE s.MentorUserId=@mentor AND a.MentorUserId=@mentor {filter} ORDER BY s.StartsAt", mentor!.Id, ct));
    }

    [HttpPost("schedules")]
    public async Task<IActionResult> CreateSchedule(MentoringScheduleRequest request, CancellationToken ct)
    {
        if (!TryMentor(out var mentor, out var error)) return error!;
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200 || request.EndsAt <= request.StartsAt) return BadRequest(new { message = "Tiêu đề hoặc khung giờ không hợp lệ." });
        await using var db = new SqlConnection(connectionString); await db.OpenAsync(ct);
        if (!await IsAssigned(db, mentor!.Id, request.InternId, ct)) return Forbid();
        await using var cmd = db.CreateCommand(); cmd.CommandText = "INSERT dbo.MentoringSchedules(InternId,MentorUserId,Title,Content,StartsAt,EndsAt,Location,Notes) OUTPUT inserted.Id VALUES(@intern,@mentor,@title,@content,@start,@end,@location,@notes)";
        AddScheduleParameters(cmd, request, mentor.Id); var id = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        await NotifyIntern(db, request.InternId, "MentoringScheduled", "Có lịch mentoring mới", request.Title.Trim(), ct);
        return Created($"/api/mentor/schedules/{id}", new { id, message = "Đã tạo lịch mentoring." });
    }

    [HttpPut("schedules/{id:int}")]
    public async Task<IActionResult> UpdateSchedule(int id, MentoringScheduleRequest request, CancellationToken ct)
    {
        if (!TryMentor(out var mentor, out var error)) return error!;
        if (string.IsNullOrWhiteSpace(request.Title) || request.EndsAt <= request.StartsAt) return BadRequest(new { message = "Tiêu đề hoặc khung giờ không hợp lệ." });
        await using var db = new SqlConnection(connectionString); await db.OpenAsync(ct);
        if (!await IsAssigned(db, mentor!.Id, request.InternId, ct)) return Forbid();
        await using var cmd = db.CreateCommand(); cmd.CommandText = "UPDATE s SET Title=@title,Content=@content,StartsAt=@start,EndsAt=@end,Location=@location,Notes=@notes FROM dbo.MentoringSchedules s JOIN dbo.MentorInternAssignments a ON a.InternId=s.InternId WHERE s.Id=@id AND s.InternId=@intern AND s.MentorUserId=@mentor AND a.MentorUserId=@mentor";
        AddScheduleParameters(cmd, request, mentor.Id); cmd.Parameters.AddWithValue("@id", id);
        return await cmd.ExecuteNonQueryAsync(ct) == 0 ? NotFound(new { message = "Không tìm thấy lịch mentoring." }) : Ok(new { message = "Đã cập nhật lịch." });
    }

    [HttpDelete("schedules/{id:int}")]
    public async Task<IActionResult> CancelSchedule(int id, CancellationToken ct)
    {
        if (!TryMentor(out var mentor, out var error)) return error!;
        await using var db = new SqlConnection(connectionString); await db.OpenAsync(ct);
        await using var cmd = db.CreateCommand(); cmd.CommandText = "UPDATE s SET Status=N'Cancelled' FROM dbo.MentoringSchedules s JOIN dbo.MentorInternAssignments a ON a.InternId=s.InternId WHERE s.Id=@id AND s.MentorUserId=@mentor AND a.MentorUserId=@mentor AND s.Status=N'Scheduled'";
        cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@mentor", mentor!.Id);
        return await cmd.ExecuteNonQueryAsync(ct) == 0 ? NotFound(new { message = "Không tìm thấy lịch có thể hủy." }) : NoContent();
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> Notifications(CancellationToken ct)
    {
        if (!TryMentor(out var mentor, out var error)) return error!;
        await using var db = new SqlConnection(connectionString); await db.OpenAsync(ct);
        return Ok(await ReadRows(db, "SELECT Id,Type,Title,Message,CreatedAt,ReadAt FROM dbo.UserNotifications WHERE UserId=@mentor ORDER BY CreatedAt DESC", mentor!.Id, ct));
    }

    [HttpPut("notifications/{id:int}/read")]
    public async Task<IActionResult> MarkNotificationRead(int id, CancellationToken ct)
    {
        if (!TryMentor(out var mentor, out var error)) return error!;
        await using var db = new SqlConnection(connectionString); await db.OpenAsync(ct);
        await using var cmd = db.CreateCommand(); cmd.CommandText = "UPDATE dbo.UserNotifications SET ReadAt=COALESCE(ReadAt,SYSUTCDATETIME()) WHERE Id=@id AND UserId=@mentor"; cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@mentor", mentor!.Id);
        return await cmd.ExecuteNonQueryAsync(ct) == 0 ? NotFound(new { message = "Không tìm thấy thông báo." }) : NoContent();
    }

    [HttpPut("notifications/read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        if (!TryMentor(out var mentor, out var error)) return error!;
        await using var db = new SqlConnection(connectionString); await db.OpenAsync(ct);
        await using var cmd = db.CreateCommand(); cmd.CommandText = "UPDATE dbo.UserNotifications SET ReadAt=SYSUTCDATETIME() WHERE UserId=@mentor AND ReadAt IS NULL"; cmd.Parameters.AddWithValue("@mentor", mentor!.Id); await cmd.ExecuteNonQueryAsync(ct); return NoContent();
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        if (!TryMentor(out var mentor, out var error)) return error!;
        await using var db = new SqlConnection(connectionString); await db.OpenAsync(ct); return Ok(await GetProfileData(db, mentor!.Id, ct));
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(MentorProfileRequest request, CancellationToken ct)
    {
        if (!TryMentor(out var mentor, out var error)) return error!;
        if (request.Phone?.Length > 30 || request.AvatarUrl?.Length > 1000 || request.Position?.Length > 150 || request.Department?.Length > 150 || request.Skills?.Length > 2000 || request.Experience?.Length > 4000) return BadRequest(new { message = "Một số trường vượt quá độ dài cho phép." });
        await using var db = new SqlConnection(connectionString); await db.OpenAsync(ct); await using var cmd = db.CreateCommand();
        cmd.CommandText = "UPDATE dbo.Users SET Phone=@phone,AvatarUrl=@avatar,Position=@position,Department=@department,Skills=@skills,Experience=@experience WHERE Id=@id AND Role='MENTOR'";
        cmd.Parameters.AddWithValue("@phone", Db(request.Phone)); cmd.Parameters.AddWithValue("@avatar", Db(request.AvatarUrl)); cmd.Parameters.AddWithValue("@position", Db(request.Position)); cmd.Parameters.AddWithValue("@department", Db(request.Department)); cmd.Parameters.AddWithValue("@skills", Db(request.Skills)); cmd.Parameters.AddWithValue("@experience", Db(request.Experience)); cmd.Parameters.AddWithValue("@id", mentor!.Id);
        if (await cmd.ExecuteNonQueryAsync(ct) == 0) return NotFound(new { message = "Không tìm thấy hồ sơ Mentor." });
        return Ok(await GetProfileData(db, mentor.Id, ct));
    }

    [HttpGet("assignment-options")]
    public async Task<IActionResult> AssignmentOptions(CancellationToken ct)
    {
        if (!TryManageInterns(out var failure)) return failure!;
        await using var db = new SqlConnection(connectionString); await db.OpenAsync(ct);
        var interns = await ReadRows(db, "SELECT Id,FullName AS Name,Mssv,Email FROM dbo.Interns ORDER BY FullName", 0, ct);
        var mentors = await ReadRows(db, "SELECT Id,FullName AS Name,Email FROM dbo.Users WHERE Role='MENTOR' ORDER BY FullName", 0, ct);
        var assignments = await ReadRows(db, "SELECT a.InternId,a.MentorUserId,u.FullName AS MentorName FROM dbo.MentorInternAssignments a JOIN dbo.Users u ON u.Id=a.MentorUserId", 0, ct);
        return Ok(new { interns, mentors, assignments });
    }

    [HttpPost("assignments")]
    public async Task<IActionResult> AssignIntern(MentorAssignmentRequest request, CancellationToken ct)
    {
        if (!TryManageInterns(out var failure)) return failure!;
        await using var db = new SqlConnection(connectionString); await db.OpenAsync(ct);
        await using var cmd = db.CreateCommand();
        cmd.CommandText = """
            IF NOT EXISTS(SELECT 1 FROM dbo.Interns WHERE Id=@intern) THROW 50001,'Intern not found',1;
            IF NOT EXISTS(SELECT 1 FROM dbo.Users WHERE Id=@mentor AND Role='MENTOR') THROW 50002,'Mentor not found',1;
            IF EXISTS(SELECT 1 FROM dbo.MentorInternAssignments WHERE InternId=@intern)
                UPDATE dbo.MentorInternAssignments SET MentorUserId=@mentor,AssignedAt=SYSUTCDATETIME() WHERE InternId=@intern;
            ELSE INSERT dbo.MentorInternAssignments(InternId,MentorUserId) VALUES(@intern,@mentor);
            UPDATE dbo.Interns SET Mentor=(SELECT FullName FROM dbo.Users WHERE Id=@mentor) WHERE Id=@intern;
            IF EXISTS(SELECT 1 FROM dbo.Interns WHERE Id=@intern AND UserId IS NOT NULL)
                INSERT dbo.UserNotifications(UserId,Type,Title,Message) SELECT @mentor,N'InternAssigned',N'Thực tập sinh mới được phân công',FullName FROM dbo.Interns WHERE Id=@intern;
            """;
        cmd.Parameters.AddWithValue("@intern", request.InternId); cmd.Parameters.AddWithValue("@mentor", request.MentorUserId);
        try { await cmd.ExecuteNonQueryAsync(ct); return Ok(new { message = "Đã phân công thực tập sinh cho Mentor." }); }
        catch (SqlException ex) when (ex.Number == 50001 || ex.Number == 50002) { return BadRequest(new { message = "Thực tập sinh hoặc tài khoản Mentor không hợp lệ." }); }
    }

    [HttpPost("assignments/bulk")]
    public async Task<IActionResult> AssignInterns(BulkMentorAssignmentRequest request, CancellationToken ct)
    {
        if (!TryManageInterns(out var failure)) return failure!;
        var internIds = request.InternIds?.Distinct().ToArray() ?? [];
        if (internIds.Length == 0 || internIds.Length > 1000)
            return BadRequest(new { message = "Chọn từ 1 đến 1.000 thực tập sinh để phân công." });

        await using var db = new SqlConnection(connectionString); await db.OpenAsync(ct);
        await using var tx = (SqlTransaction)await db.BeginTransactionAsync(ct);
        try
        {
            await using var verifyMentor = db.CreateCommand(); verifyMentor.Transaction = tx;
            verifyMentor.CommandText = "SELECT FullName FROM dbo.Users WHERE Id=@mentor AND Role='MENTOR'";
            verifyMentor.Parameters.AddWithValue("@mentor", request.MentorUserId);
            var mentorName = await verifyMentor.ExecuteScalarAsync(ct) as string;
            if (mentorName is null) { await tx.RollbackAsync(ct); return BadRequest(new { message = "Tài khoản Mentor không hợp lệ." }); }

            foreach (var internId in internIds)
            {
                await using var assign = db.CreateCommand(); assign.Transaction = tx;
                assign.CommandText = "IF NOT EXISTS(SELECT 1 FROM dbo.Interns WHERE Id=@intern) THROW 50001,'Intern not found',1; IF EXISTS(SELECT 1 FROM dbo.MentorInternAssignments WHERE InternId=@intern) UPDATE dbo.MentorInternAssignments SET MentorUserId=@mentor,AssignedAt=SYSUTCDATETIME() WHERE InternId=@intern; ELSE INSERT dbo.MentorInternAssignments(InternId,MentorUserId) VALUES(@intern,@mentor); UPDATE dbo.Interns SET Mentor=@mentorName WHERE Id=@intern; INSERT dbo.UserNotifications(UserId,Type,Title,Message) SELECT @mentor,N'InternAssigned',N'Thực tập sinh được phân công',FullName FROM dbo.Interns WHERE Id=@intern AND UserId IS NOT NULL";
                assign.Parameters.AddWithValue("@intern", internId); assign.Parameters.AddWithValue("@mentor", request.MentorUserId); assign.Parameters.AddWithValue("@mentorName", mentorName);
                await assign.ExecuteNonQueryAsync(ct);
            }
            await tx.CommitAsync(ct);
            return Ok(new { assignedCount = internIds.Length, message = $"Đã phân công {internIds.Length} thực tập sinh vào nhóm của {mentorName}." });
        }
        catch (SqlException ex) when (ex.Number == 50001)
        {
            await tx.RollbackAsync(ct);
            return BadRequest(new { message = "Một hoặc nhiều hồ sơ thực tập sinh không tồn tại; chưa có phân công nào được thay đổi." });
        }
        catch { await tx.RollbackAsync(ct); throw; }
    }

    private bool TryMentor(out AuthenticatedUser? user, out IActionResult? failure)
    {
        user = null; var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) || !tokens.TryValidate(header[7..].Trim(), out user))
        { failure = Unauthorized(new { message = "Vui lòng đăng nhập." }); return false; }
        if (!RolePermissions.HasPermission(user!.Role, "progress.review"))
        { failure = StatusCode(403, new { message = "Bạn không có quyền truy cập chức năng Mentor." }); return false; }
        failure = null; return true;
    }

    private bool TryManageInterns(out IActionResult? failure)
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) || !tokens.TryValidate(header[7..].Trim(), out var user))
        { failure = Unauthorized(new { message = "Vui lòng đăng nhập." }); return false; }
        if (!RolePermissions.HasPermission(user!.Role, "interns.manage"))
        { failure = StatusCode(403, new { message = "Bạn không có quyền phân công thực tập sinh." }); return false; }
        failure = null; return true;
    }

    private static async Task<bool> IsAssigned(SqlConnection db, int mentorId, int internId, CancellationToken ct)
    { await using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT COUNT(1) FROM dbo.MentorInternAssignments WHERE MentorUserId=@mentor AND InternId=@intern"; cmd.Parameters.AddWithValue("@mentor", mentorId); cmd.Parameters.AddWithValue("@intern", internId); return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) > 0; }

    private static void AddTaskParameters(SqlCommand cmd, MentorTaskRequest request, int mentorId)
    { cmd.Parameters.AddWithValue("@intern", request.InternId); cmd.Parameters.AddWithValue("@mentor", mentorId); cmd.Parameters.AddWithValue("@title", request.Title!.Trim()); cmd.Parameters.AddWithValue("@description", Db(request.Description)); cmd.Parameters.AddWithValue("@deadline", (object?)request.Deadline?.Date ?? DBNull.Value); cmd.Parameters.AddWithValue("@priority", request.Priority!); }
    private static void AddScheduleParameters(SqlCommand cmd, MentoringScheduleRequest r, int mentorId)
    { cmd.Parameters.AddWithValue("@intern", r.InternId); cmd.Parameters.AddWithValue("@mentor", mentorId); cmd.Parameters.AddWithValue("@title", r.Title!.Trim()); cmd.Parameters.AddWithValue("@content", Db(r.Content)); cmd.Parameters.AddWithValue("@start", r.StartsAt); cmd.Parameters.AddWithValue("@end", r.EndsAt); cmd.Parameters.AddWithValue("@location", Db(r.Location)); cmd.Parameters.AddWithValue("@notes", Db(r.Notes)); }
    private static bool ValidPriority(string? value) => value is "Low" or "Medium" or "High";
    private static object Db(string? value) => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

    private static async Task NotifyIntern(SqlConnection db, int internId, string type, string title, string message, CancellationToken ct, SqlTransaction? tx = null)
    { await using var cmd = db.CreateCommand(); cmd.Transaction = tx; cmd.CommandText = "INSERT dbo.UserNotifications(UserId,Type,Title,Message) SELECT UserId,@type,@title,@message FROM dbo.Interns WHERE Id=@intern AND UserId IS NOT NULL"; cmd.Parameters.AddWithValue("@intern", internId); cmd.Parameters.AddWithValue("@type", type); cmd.Parameters.AddWithValue("@title", title.Length > 200 ? title[..200] : title); cmd.Parameters.AddWithValue("@message", message.Length > 1000 ? message[..1000] : message); await cmd.ExecuteNonQueryAsync(ct); }

    private static async Task<Dictionary<string, object?>> GetProfileData(SqlConnection db, int id, CancellationToken ct)
    { var list = await ReadRows(db, "SELECT Id,Username,FullName,Email,Phone,AvatarUrl,Position,Department,Skills,Experience,Role FROM dbo.Users WHERE Id=@id AND Role='MENTOR'", id, ct, ("@id", id)); return list.FirstOrDefault() ?? new Dictionary<string, object?>(); }

    private static async Task<List<Dictionary<string, object?>>> ReadRows(SqlConnection db, string sql, int mentorId, CancellationToken ct, params (string Name, object? Value)[] extra)
    { await using var cmd = db.CreateCommand(); cmd.CommandText = sql; cmd.Parameters.AddWithValue("@mentor", mentorId); foreach (var p in extra) cmd.Parameters.AddWithValue(p.Name, p.Value ?? DBNull.Value); return await ReadRows(cmd, ct); }
    private static async Task<List<Dictionary<string, object?>>> ReadRows(SqlCommand cmd, CancellationToken ct)
    { var rows = new List<Dictionary<string, object?>>(); await using var reader = await cmd.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) { var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase); for (var i=0;i<reader.FieldCount;i++) row[reader.GetName(i)] = await reader.IsDBNullAsync(i, ct) ? null : reader.GetValue(i); rows.Add(row); } return rows; }
}
