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
        return authorization.Evaluate(Request, permission);
    }
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    { var access=Access(PermissionNames.ReadMeetings); return access.Status!=AuthorizationStatus.Authorized?AuthorizationResponses.Denied(access.Status):Ok(await meetings.GetAllAsync(ct,access.User!.Id)); }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id,CancellationToken ct)
    {
        var access=Access(PermissionNames.ReadMeetings);
        if(access.Status!=AuthorizationStatus.Authorized)return AuthorizationResponses.Denied(access.Status);
        var meeting=(await meetings.GetAllAsync(ct,access.User!.Id)).FirstOrDefault(x=>x.Id==id);
        return meeting is null?StatusCode(403):Ok(meeting);
    }
    [HttpGet("participants")]
    public async Task<IActionResult> Participants(CancellationToken ct)
    { var access=Access(PermissionNames.CreateMeeting); return access.Status!=AuthorizationStatus.Authorized?AuthorizationResponses.Denied(access.Status):Ok(await meetings.GetParticipantsAsync(ct)); }

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
