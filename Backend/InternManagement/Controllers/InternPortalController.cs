using InternManagement.Models;
using InternManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace InternManagement.Controllers;

[ApiController]
[Route("api/intern-portal")]
public sealed class InternPortalController : ControllerBase
{
    private readonly string connectionString;
    private readonly AuthTokenService tokens;

    public InternPortalController(IConfiguration configuration, AuthTokenService tokens)
    {
        connectionString = configuration.GetConnectionString("InternManagement") ?? throw new InvalidOperationException("ConnectionStrings:InternManagement is missing.");
        this.tokens = tokens;
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        if (!TryIntern(out var user, out var error)) return error!;
        await using var db = new SqlConnection(connectionString); await db.OpenAsync(ct);
        var rows = await ReadRows(db, "SELECT i.Id,i.FullName,i.Mssv,i.Email,i.Phone,i.School,i.Major,i.InternshipPosition,i.Department,i.StartDate,i.EndDate,i.Status,i.Progress,m.FullName AS MentorName,m.Email AS MentorEmail FROM dbo.Interns i LEFT JOIN dbo.MentorInternAssignments a ON a.InternId=i.Id LEFT JOIN dbo.Users m ON m.Id=a.MentorUserId WHERE i.UserId=@user", user!.Id, ct);
        return rows.Count == 0 ? NotFound(new { message = "Tài khoản Intern chưa được liên kết với hồ sơ thực tập. Nhờ HR liên kết email tài khoản với hồ sơ." }) : Ok(rows[0]);
    }

    [HttpGet("tasks")]
    public async Task<IActionResult> Tasks(CancellationToken ct)
    {
        if (!TryIntern(out var user, out var error)) return error!;
        await using var db = new SqlConnection(connectionString); await db.OpenAsync(ct);
        return Ok(await ReadRows(db, "SELECT t.Id,t.Title,t.Description,t.AssignedAt,t.Deadline,t.Priority,t.Status,m.FullName AS MentorName FROM dbo.InternTasks t JOIN dbo.Interns i ON i.Id=t.InternId JOIN dbo.Users m ON m.Id=t.MentorUserId WHERE i.UserId=@user ORDER BY t.AssignedAt DESC", user!.Id, ct));
    }

    [HttpPut("tasks/{id:int}/status")]
    public async Task<IActionResult> UpdateTaskStatus(int id, TaskStatusRequest request, CancellationToken ct)
    {
        if (!TryIntern(out var user, out var error)) return error!;
        if (request.Status is not ("InProgress" or "PendingReview")) return BadRequest(new { message = "Trạng thái nhiệm vụ không hợp lệ." });
        await using var db = new SqlConnection(connectionString); await db.OpenAsync(ct);
        await using var cmd = db.CreateCommand(); cmd.CommandText = "UPDATE t SET Status=@status FROM dbo.InternTasks t JOIN dbo.Interns i ON i.Id=t.InternId WHERE t.Id=@id AND i.UserId=@user AND t.Status<>N'Completed'";
        cmd.Parameters.AddWithValue("@status", request.Status); cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@user", user!.Id);
        if (await cmd.ExecuteNonQueryAsync(ct) == 0) return NotFound(new { message = "Không tìm thấy nhiệm vụ." });
        await NotifyMentor(db, id, "TaskUpdate", "Thực tập sinh cập nhật nhiệm vụ", $"Nhiệm vụ được chuyển sang {request.Status}.", true, ct);
        return Ok(new { message = "Đã cập nhật trạng thái nhiệm vụ." });
    }

    [HttpGet("reports")]
    public async Task<IActionResult> Reports(CancellationToken ct)
    {
        if (!TryIntern(out var user, out var error)) return error!;
        await using var db = new SqlConnection(connectionString); await db.OpenAsync(ct);
        return Ok(await ReadRows(db, "SELECT Id,Title,Content,AttachmentUrl,Status,SubmittedAt FROM dbo.InternReports WHERE InternId=(SELECT Id FROM dbo.Interns WHERE UserId=@user) ORDER BY SubmittedAt DESC", user!.Id, ct));
    }

    [HttpPost("reports")]
    public async Task<IActionResult> SubmitReport(ReportSubmission request, CancellationToken ct)
    {
        if (!TryIntern(out var user, out var error)) return error!;
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200 || string.IsNullOrWhiteSpace(request.Content) || request.Content.Length > 50000 || request.AttachmentUrl?.Length > 1000)
            return BadRequest(new { message = "Tiêu đề hoặc nội dung báo cáo không hợp lệ." });
        await using var db = new SqlConnection(connectionString); await db.OpenAsync(ct);
        var intern = await InternId(db, user!.Id, ct); if (!intern.HasValue) return NotFound(new { message = "Tài khoản chưa được liên kết với hồ sơ thực tập." });
        await using var cmd = db.CreateCommand(); cmd.CommandText = "INSERT dbo.InternReports(InternId,Title,Content,AttachmentUrl) VALUES(@intern,@title,@content,@attachment)";
        cmd.Parameters.AddWithValue("@intern", intern.Value); cmd.Parameters.AddWithValue("@title", request.Title.Trim()); cmd.Parameters.AddWithValue("@content", request.Content.Trim()); cmd.Parameters.AddWithValue("@attachment", Db(request.AttachmentUrl)); await cmd.ExecuteNonQueryAsync(ct);
        await NotifyMentor(db, intern.Value, "ReportSubmitted", "Có báo cáo thực tập mới", request.Title.Trim(), false, ct);
        return Ok(new { message = "Đã nộp báo cáo." });
    }

    [HttpGet("feedback")]
    public async Task<IActionResult> Feedback(CancellationToken ct)
    {
        if (!TryIntern(out var user, out var error)) return error!;
        await using var db = new SqlConnection(connectionString); await db.OpenAsync(ct);
        return Ok(await ReadRows(db, "SELECT f.Id,f.Content,f.Type,f.Severity,f.CreatedAt,m.FullName AS MentorName FROM dbo.InternFeedback f JOIN dbo.Interns i ON i.Id=f.InternId JOIN dbo.Users m ON m.Id=f.MentorUserId WHERE i.UserId=@user ORDER BY f.CreatedAt DESC", user!.Id, ct));
    }

    [HttpGet("evaluations")]
    public async Task<IActionResult> Evaluations(CancellationToken ct)
    {
        if (!TryIntern(out var user, out var error)) return error!;
        await using var db = new SqlConnection(connectionString); await db.OpenAsync(ct);
        var evaluations = await ReadRows(db, "SELECT e.Id,e.Comments,e.Recommendation,e.IsSubmitted,e.UpdatedAt,m.FullName AS MentorName FROM dbo.InternEvaluations e JOIN dbo.Interns i ON i.Id=e.InternId JOIN dbo.Users m ON m.Id=e.MentorUserId WHERE i.UserId=@user AND e.IsSubmitted=1 ORDER BY e.UpdatedAt DESC", user!.Id, ct);
        foreach (var row in evaluations) row["scores"] = await ReadRows(db, "SELECT s.Criterion,s.Score FROM dbo.InternEvaluationScores s WHERE s.EvaluationId=@id", Convert.ToInt32(row["Id"]), ct, ("@id", row["Id"]));
        return Ok(evaluations);
    }

    [HttpGet("schedules")]
    public async Task<IActionResult> Schedules(CancellationToken ct)
    {
        if (!TryIntern(out var user, out var error)) return error!;
        await using var db = new SqlConnection(connectionString); await db.OpenAsync(ct);
        return Ok(await ReadRows(db, "SELECT s.Id,s.Title,s.Content,s.StartsAt,s.EndsAt,s.Location,s.Notes,s.Status,m.FullName AS MentorName FROM dbo.MentoringSchedules s JOIN dbo.Interns i ON i.Id=s.InternId JOIN dbo.Users m ON m.Id=s.MentorUserId WHERE i.UserId=@user AND s.Status=N'Scheduled' ORDER BY s.StartsAt", user!.Id, ct));
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> Notifications(CancellationToken ct)
    {
        if (!TryIntern(out var user, out var error)) return error!;
        await using var db = new SqlConnection(connectionString); await db.OpenAsync(ct);
        return Ok(await ReadRows(db, "SELECT Id,Type,Title,Message,CreatedAt,ReadAt FROM dbo.UserNotifications WHERE UserId=@user ORDER BY CreatedAt DESC", user!.Id, ct));
    }

    [HttpPut("notifications/{id:int}/read")]
    public async Task<IActionResult> MarkNotificationRead(int id, CancellationToken ct)
    {
        if (!TryIntern(out var user, out var error)) return error!;
        await using var db = new SqlConnection(connectionString); await db.OpenAsync(ct);
        await using var cmd = db.CreateCommand(); cmd.CommandText = "UPDATE dbo.UserNotifications SET ReadAt=COALESCE(ReadAt,SYSUTCDATETIME()) WHERE Id=@id AND UserId=@user"; cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@user", user!.Id);
        return await cmd.ExecuteNonQueryAsync(ct) == 0 ? NotFound(new { message = "Không tìm thấy thông báo." }) : NoContent();
    }

    private bool TryIntern(out AuthenticatedUser? user, out IActionResult? failure)
    {
        user = null; var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) || !tokens.TryValidate(header[7..].Trim(), out user))
        { failure = Unauthorized(new { message = "Vui lòng đăng nhập." }); return false; }
        if (!string.Equals(user!.Role, "INTERN", StringComparison.OrdinalIgnoreCase))
        { failure = StatusCode(403, new { message = "Chỉ tài khoản Intern mới được truy cập." }); return false; }
        failure = null; return true;
    }

    private static async Task<int?> InternId(SqlConnection db, int userId, CancellationToken ct)
    { await using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT Id FROM dbo.Interns WHERE UserId=@user"; cmd.Parameters.AddWithValue("@user", userId); var value = await cmd.ExecuteScalarAsync(ct); return value is null ? null : Convert.ToInt32(value); }

    private static async Task NotifyMentor(SqlConnection db, int id, string type, string title, string message, bool taskId, CancellationToken ct)
    {
        var sql = taskId
            ? "INSERT dbo.UserNotifications(UserId,Type,Title,Message) SELECT t.MentorUserId,@type,@title,@message FROM dbo.InternTasks t WHERE t.Id=@id"
            : "INSERT dbo.UserNotifications(UserId,Type,Title,Message) SELECT a.MentorUserId,@type,@title,@message FROM dbo.MentorInternAssignments a WHERE a.InternId=@id";
        await using var cmd = db.CreateCommand(); cmd.CommandText = sql; cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@type", type); cmd.Parameters.AddWithValue("@title", title); cmd.Parameters.AddWithValue("@message", message); await cmd.ExecuteNonQueryAsync(ct);
    }

    private static object Db(string? value) => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    private static async Task<List<Dictionary<string, object?>>> ReadRows(SqlConnection db, string sql, int id, CancellationToken ct, params (string Name, object? Value)[] extra)
    { await using var cmd = db.CreateCommand(); cmd.CommandText = sql; cmd.Parameters.AddWithValue("@user", id); cmd.Parameters.AddWithValue("@id", id); foreach (var p in extra) cmd.Parameters.AddWithValue(p.Name,p.Value ?? DBNull.Value); var rows = new List<Dictionary<string, object?>>(); await using var reader = await cmd.ExecuteReaderAsync(ct); while(await reader.ReadAsync(ct)){var row=new Dictionary<string,object?>(StringComparer.OrdinalIgnoreCase);for(var i=0;i<reader.FieldCount;i++)row[reader.GetName(i)]=await reader.IsDBNullAsync(i,ct)?null:reader.GetValue(i);rows.Add(row);}return rows; }

    public sealed record TaskStatusRequest(string? Status);
    public sealed record ReportSubmission(string? Title, string? Content, string? AttachmentUrl);
}
