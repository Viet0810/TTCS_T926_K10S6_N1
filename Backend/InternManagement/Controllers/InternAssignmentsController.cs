using InternManagement.DTOs;
using InternManagement.Infrastructure;
using InternManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace InternManagement.Controllers;

[ApiController]
[Route("api/intern-assignments")]
public sealed class InternAssignmentsController(
    InternAssignmentService assignments, ProgramScheduleService programs,
    RequestAuthorizationService authorization) : ControllerBase
{
    private IActionResult? Denied()
    {
        var decision = authorization.Evaluate(Request, PermissionNames.AssignMentor);
        if (decision.Status != AuthorizationStatus.Authorized) return AuthorizationResponses.Denied(decision.Status);
        return decision.User!.Role == "HR" ? null : AuthorizationResponses.Denied(AuthorizationStatus.Forbidden);
    }

    [HttpGet]
    public async Task<IActionResult> GetAssignments(CancellationToken cancellationToken) =>
        Denied() ?? Ok(await assignments.GetInternsAsync(true, cancellationToken));

    [HttpGet("interns")]
    public async Task<IActionResult> GetInterns(CancellationToken cancellationToken) =>
        Denied() ?? Ok(await assignments.GetInternsAsync(false, cancellationToken));

    [HttpGet("mentors")]
    public async Task<IActionResult> GetMentors(CancellationToken cancellationToken) =>
        Denied() ?? Ok(await assignments.GetMentorsAsync(cancellationToken));

    [HttpGet("programs")]
    public async Task<IActionResult> GetPrograms() => Denied() ?? Ok(await programs.GetAllAsync());

    [HttpPut("{internId:int}")]
    public async Task<IActionResult> Assign(int internId, AssignMentorRequest request, CancellationToken cancellationToken)
    {
        if (Denied() is { } denied) return denied;
        try
        {
            await assignments.AssignAsync(internId, request, cancellationToken);
            return Ok(new { success = true, message = "Đã lưu phân công Mentor." });
        }
        catch (AssignmentValidationException error)
        {
            var response = new ApiErrorResponse(false, error.Message, null);
            return error.NotFound ? NotFound(response) : BadRequest(response);
        }
    }
}
