using InternManagement.Infrastructure;
using InternManagement.DTOs;
using InternManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace InternManagement.Controllers;

/// <summary>
/// Chức năng: Báo cáo chuyên cần & nghỉ phép (K10S6N1-91 / K10S6N1-60)
/// Controller API cung cấp dữ liệu báo cáo chuyên cần, nghỉ phép và bộ lọc cho HR
/// </summary>
[ApiController]
[Route("api/attendance/report")]
public sealed class AttendanceReportController : ControllerBase
{
    private readonly IAttendanceReportService reportService;
    private readonly RequestAuthorizationService authorization;

    public AttendanceReportController(
        IAttendanceReportService reportService,
        RequestAuthorizationService authorization)
    {
        this.reportService = reportService;
        this.authorization = authorization;
    }

    /// <summary>
    /// Lấy danh sách ca làm việc và tổng hợp chỉ số KPI chuyên cần theo các tiêu chí lọc
    /// </summary>
    [HttpGet]
    [ProducesResponseType<AttendanceReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetReport(
        [FromQuery] AttendanceReportRequest? filter,
        CancellationToken cancellationToken)
    {
        var decision = authorization.Evaluate(Request, PermissionNames.ViewAttendanceReport);
        if (decision.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(decision.Status);
        if (decision.User!.Role != "HR") return AuthorizationResponses.Denied(AuthorizationStatus.Forbidden);

        var report = await reportService.GetReportAsync(filter, cancellationToken);
        return Ok(report);
    }

    /// <summary>
    /// Ghi nhận một ca điểm danh / làm việc mới cho thực tập sinh
    /// </summary>
    [HttpPost("record")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RecordAttendance(
        [FromBody] CreateAttendanceRecordRequest request,
        CancellationToken cancellationToken)
    {
        var decision = authorization.Evaluate(Request, PermissionNames.ManageInterns);
        if (decision.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(decision.Status);
        if (decision.User!.Role != "HR") return AuthorizationResponses.Denied(AuthorizationStatus.Forbidden);

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var id = await reportService.RecordAttendanceAsync(request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, new { id, message = "Ghi nhận ca làm việc thành công." });
        }
        catch (ArgumentException error) { return BadRequest(new { message = error.Message }); }
    }

    /// <summary>
    /// Nạp dữ liệu mẫu điểm danh phục vụ kiểm thử và demo nếu cơ sở dữ liệu chưa có dữ liệu
    /// </summary>
    [HttpPost("seed")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SeedSampleData(CancellationToken cancellationToken)
    {
        var decision = authorization.Evaluate(Request, PermissionNames.ManageInterns);
        if (decision.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(decision.Status);
        if (decision.User!.Role != "HR") return AuthorizationResponses.Denied(AuthorizationStatus.Forbidden);

        var inserted = await reportService.SeedSampleDataAsync(cancellationToken);
        return Ok(new { inserted, message = $"Đã nạp {inserted} bản ghi mẫu điểm danh." });
    }
}
