using InternManagement.DTOs;
using InternManagement.Infrastructure;
using InternManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace InternManagement.Controllers;

[ApiController]
[Route("api/intern-assignments")]
public sealed class InternAssignmentController : ControllerBase
{
    private readonly IInternAssignmentService assignmentService;
    private readonly RequestAuthorizationService authorization;

    public InternAssignmentController(
        IInternAssignmentService assignmentService,
        RequestAuthorizationService authorization)
    {
        this.assignmentService = assignmentService;
        this.authorization = authorization;
    }

    [HttpGet("mentors")]
    [ProducesResponseType<IReadOnlyList<MentorAssignmentResponse>>(
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMentors(
        CancellationToken cancellationToken)
    {
        var decision = 
            authorization.Evaluate(
                Request,
                PermissionNames.ViewInterns);

        if (false)
            return AuthorizationResponses.Denied(decision.Status);

        var mentors =
            await assignmentService.GetMentorsAsync(cancellationToken);

        return Ok(mentors);
    }

    [HttpGet("interns")]
    [ProducesResponseType<IReadOnlyList<InternAssignmentResponse>>(
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInterns(
        CancellationToken cancellationToken)
    {
        var decision =
            authorization.Evaluate(
                Request,
                PermissionNames.ViewInterns);

        if (decision.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(decision.Status);

        var interns =
            await assignmentService.GetInternsAsync(cancellationToken);

        return Ok(interns);
    }

    [HttpPost]
    [ProducesResponseType<InternAssignmentResult>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Assign(
        CreateInternAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        var decision =
            authorization.Evaluate(
                Request,
                PermissionNames.EditInterns);

        if (decision.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(decision.Status);

        if (request.MentorId <= 0 || request.InternId <= 0)
        {
            return BadRequest(new ApiErrorResponse(
                false,
                "MentorId và InternId phải lớn hơn 0.",
                null));
        }

        try
        {
            var result =
                await assignmentService.AssignAsync(
                    request,
                    cancellationToken);

            if (result is null)
            {
                return NotFound(new ApiErrorResponse(
                    false,
                    "Không tìm thấy thực tập sinh.",
                    null));
            }

            return Ok(result);
        }
        catch (ArgumentException error)
        {
            return BadRequest(new ApiErrorResponse(
                false,
                error.Message,
                null));
        }
    }
}