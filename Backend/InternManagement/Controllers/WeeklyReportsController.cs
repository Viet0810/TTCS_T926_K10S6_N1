using InternManagement.DTOs;
using InternManagement.Infrastructure;
using InternManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace InternManagement.Controllers;

[ApiController]
[Route("api/weekly-reports")]
public sealed class WeeklyReportsController(
    WeeklyReportService reports,
    NotificationService notifications,
    ILogger<WeeklyReportsController> logger,
    RequestAuthorizationService authorization) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> ListMine(CancellationToken cancellationToken)
    {
        var decision = authorization.Evaluate(Request, PermissionNames.SubmitWeeklyReports);
        if (decision.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(decision.Status);
        var internId = await reports.GetInternIdForUserAsync(decision.User!.Id, cancellationToken);
        if (internId is null) return NotFound(new { message = "Chưa có hồ sơ thực tập sinh gắn với tài khoản này." });
        return Ok(await reports.ListForInternAsync(internId.Value, cancellationToken));
    }

    [HttpPost("me")]
    public async Task<IActionResult> Submit(CreateWeeklyReportRequest request, CancellationToken cancellationToken)
    {
        var decision = authorization.Evaluate(Request, PermissionNames.SubmitWeeklyReports);
        if (decision.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(decision.Status);
        if (request.WeekStart is null || request.WeekEnd is null || request.WeekEnd.Value < request.WeekStart.Value
            || request.WeekEnd.Value.DayNumber - request.WeekStart.Value.DayNumber > 6)
            return BadRequest(new { message = "Khoảng thời gian báo cáo phải nằm trong cùng một tuần (tối đa 7 ngày)." });
        if (string.IsNullOrWhiteSpace(request.Summary))
            return BadRequest(new { message = "Vui lòng nhập phần tổng kết tuần." });

        var internId = await reports.GetInternIdForUserAsync(decision.User!.Id, cancellationToken);
        if (internId is null) return NotFound(new { message = "Chưa có hồ sơ thực tập sinh gắn với tài khoản này." });
        if (!await reports.HasAssignedMentorAsync(internId.Value, cancellationToken))
            return Conflict(new { message = "Hồ sơ của bạn chưa được gắn với email tài khoản MENTOR. Báo cáo chưa được gửi; vui lòng liên hệ HR cập nhật mục Email người hướng dẫn." });
        try
        {
            var savedId = await reports.SaveForInternAsync(internId.Value, request, cancellationToken);
            if (savedId is null)
                return Conflict(new { message = "Mentor đã phản hồi báo cáo tuần này nên bạn không thể thay đổi nội dung." });
            var id = savedId.Value;
            bool notificationCreated;
            try { notificationCreated = await notifications.NotifyMentorOfWeeklyReportAsync(id, cancellationToken); }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                logger.LogWarning(error, "Weekly report {ReportId} was saved but the Mentor notification could not be created.", id);
                notificationCreated = false;
            }
            return Created("/api/weekly-reports/me", new
            {
                id,
                notificationCreated,
                message = notificationCreated
                    ? "Đã gửi báo cáo tuần và thông báo cho Mentor."
                    : "Đã lưu báo cáo. Chưa tạo được thông báo; vui lòng báo HR kiểm tra cấu hình Mentor."
            });
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(error, "Could not save weekly report for intern {InternId}.", internId);
            throw;
        }
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var decision = authorization.Evaluate(Request, PermissionNames.ViewWeeklyReports);
        if (decision.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(decision.Status);

        return Ok(await reports.ListForMentorAsync(decision.User!.Id, cancellationToken));
    }

    [HttpPut("{id:long}/feedback")]
    public async Task<IActionResult> SaveFeedback(long id, SaveWeeklyReportFeedbackRequest request, CancellationToken cancellationToken)
    {
        var decision = authorization.Evaluate(Request, PermissionNames.ViewWeeklyReports);
        if (decision.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(decision.Status);
        if (id <= 0) return BadRequest(new { message = "Mã báo cáo không hợp lệ." });
        if (string.IsNullOrWhiteSpace(request.Feedback))
            return BadRequest(new { message = "Vui lòng nhập phản hồi." });

        var mentorId = decision.User!.Id;
        var saved = await reports.SaveFeedbackAsync(id, mentorId, request.Feedback, cancellationToken);
        if (!saved) return NotFound(new { message = "Không tìm thấy báo cáo được giao cho bạn." });
        bool notificationCreated;
        try { notificationCreated = await notifications.NotifyInternOfWeeklyFeedbackAsync(id, mentorId, cancellationToken); }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogWarning(error, "Mentor feedback for weekly report {ReportId} was saved but the intern notification could not be created.", id);
            notificationCreated = false;
        }
        return Ok(new { message = "Đã lưu phản hồi.", notificationCreated });
    }
}
