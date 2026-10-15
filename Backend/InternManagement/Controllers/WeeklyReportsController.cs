using InternManagement.DTOs;
using InternManagement.Infrastructure;
using InternManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace InternManagement.Controllers;

[ApiController]
[Route("api/weekly-reports")]
public sealed class WeeklyReportsController(WeeklyReportService reports, RequestAuthorizationService authorization) : ControllerBase
{
    private AuthorizationDecision Access(string? action = null)
    {
        var access = authorization.Evaluate(Request);
        if (access.Status != AuthorizationStatus.Authorized) return access;
        var role = access.User!.Role;
        if (role is not ("MENTOR" or "INTERN")) return access with { Status = AuthorizationStatus.Forbidden };
        access = authorization.Evaluate(Request, role == "MENTOR" ? PermissionNames.ReadMentorWeeklyReports : PermissionNames.ReadOwnWeeklyReports);
        if (access.Status != AuthorizationStatus.Authorized || action is null) return access;
        if ((action == PermissionNames.CreateWeeklyReport && role != "INTERN") ||
            (action == PermissionNames.CreateWeeklyReportFeedback && role != "MENTOR"))
            return access with { Status = AuthorizationStatus.Forbidden };
        return authorization.Evaluate(Request,action);
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var access = Access();
        if (access.Status != AuthorizationStatus.Authorized) return AuthorizationResponses.Denied(access.Status);
        return Ok(await reports.GetAsync(access.User!.Id,access.User.Role == "MENTOR",null,ct));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var access = Access();
        if (access.Status != AuthorizationStatus.Authorized) return AuthorizationResponses.Denied(access.Status);
        var results = await reports.GetAsync(access.User!.Id,access.User.Role == "MENTOR",id,ct);
        return results.Count == 0 ? AuthorizationResponses.Denied(AuthorizationStatus.Forbidden) : Ok(results[0]);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateWeeklyReportRequest request, CancellationToken ct)
    {
        var access = Access(PermissionNames.CreateWeeklyReport);
        if (access.Status != AuthorizationStatus.Authorized) return AuthorizationResponses.Denied(access.Status);
        try
        {
            var id = await reports.CreateAsync(access.User!.Id,request,ct);
            return id is null ? AuthorizationResponses.Denied(AuthorizationStatus.Forbidden)
                : CreatedAtAction(nameof(Get),new { id },new { id,message = "Đã gửi báo cáo tuần." });
        }
        catch (ArgumentException error) { return BadRequest(new { message = error.Message }); }
        catch (InvalidOperationException error) { return Conflict(new { message = error.Message }); }
    }

    [HttpPost("{id:int}/feedback")]
    public async Task<IActionResult> Feedback(int id, CreateWeeklyReportFeedbackRequest request, CancellationToken ct)
    {
        var access = Access(PermissionNames.CreateWeeklyReportFeedback);
        if (access.Status != AuthorizationStatus.Authorized) return AuthorizationResponses.Denied(access.Status);
        try
        {
            var feedbackId = await reports.AddFeedbackAsync(access.User!.Id,id,request,ct);
            return feedbackId is null ? AuthorizationResponses.Denied(AuthorizationStatus.Forbidden)
                : StatusCode(StatusCodes.Status201Created,new { id=feedbackId,message = "Đã gửi phản hồi." });
        }
        catch (ArgumentException error) { return BadRequest(new { message = error.Message }); }
    }
}
