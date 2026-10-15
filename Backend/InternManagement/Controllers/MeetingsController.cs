using InternManagement.DTOs;
using InternManagement.Infrastructure;
using InternManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace InternManagement.Controllers;

[ApiController]
[Route("api/meetings")]
public sealed class MeetingsController(MeetingService meetings, RequestAuthorizationService authorization) : ControllerBase
{
    private AuthorizationDecision Access(string permission)
    {
        var access = authorization.Evaluate(Request, permission);
        return access.Status == AuthorizationStatus.Authorized && access.User!.Role != "HR"
            ? access with { Status = AuthorizationStatus.Forbidden } : access;
    }
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    { var access=Access(PermissionNames.ReadMeetings); return access.Status!=AuthorizationStatus.Authorized?AuthorizationResponses.Denied(access.Status):Ok(await meetings.GetAllAsync(ct)); }

    [HttpGet("participants")]
    public async Task<IActionResult> Participants(CancellationToken ct)
    { var access=Access(PermissionNames.ReadMeetings); return access.Status!=AuthorizationStatus.Authorized?AuthorizationResponses.Denied(access.Status):Ok(await meetings.GetParticipantsAsync(ct)); }

    [HttpPost]
    public async Task<IActionResult> Create(CreateMeetingRequest request, CancellationToken ct)
    {
        var access = Access(PermissionNames.CreateMeeting);
        if (access.Status != AuthorizationStatus.Authorized) return AuthorizationResponses.Denied(access.Status);
        try
        {
            var result = await meetings.CreateAsync(access.User!.Id, request, ct);
            return StatusCode(StatusCodes.Status201Created, result);
        }
        catch (ArgumentException error) { return BadRequest(new { message = error.Message }); }
    }
}
