using InternManagement.DTOs;
using InternManagement.Infrastructure;
using InternManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace InternManagement.Controllers;

[ApiController]
[Route("api/tasks")]
public sealed class TasksController(InternTaskService tasks, RequestAuthorizationService authorization) : ControllerBase
{
    private AuthorizationDecision Access(bool create = false)
    {
        var access = authorization.Evaluate(Request);
        if (access.Status != AuthorizationStatus.Authorized) return access;
        var role = access.User!.Role;
        if (role != "MENTOR" && (role != "INTERN" || create)) return access with { Status = AuthorizationStatus.Forbidden };
        return authorization.Evaluate(Request, create ? PermissionNames.CreateTask
            : role == "MENTOR" ? PermissionNames.ReadMentorTasks : PermissionNames.ReadOwnTasks);
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var access = Access();
        if (access.Status != AuthorizationStatus.Authorized) return AuthorizationResponses.Denied(access.Status);
        return Ok(await tasks.GetAsync(access.User!.Id, access.User.Role == "MENTOR", null, ct));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var access = Access();
        if (access.Status != AuthorizationStatus.Authorized) return AuthorizationResponses.Denied(access.Status);
        var result = await tasks.GetAsync(access.User!.Id, access.User.Role == "MENTOR", id, ct);
        return result.Count == 0 ? AuthorizationResponses.Denied(AuthorizationStatus.Forbidden) : Ok(result[0]);
    }

    [HttpGet("interns")]
    public async Task<IActionResult> Interns(CancellationToken ct)
    {
        var access = Access(create: true);
        if (access.Status != AuthorizationStatus.Authorized) return AuthorizationResponses.Denied(access.Status);
        return Ok(await tasks.GetInternsAsync(access.User!.Id, ct));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateInternTaskRequest request, CancellationToken ct)
    {
        var access = Access(create: true);
        if (access.Status != AuthorizationStatus.Authorized) return AuthorizationResponses.Denied(access.Status);
        // Action permission must retain its read dependency even with inconsistent DB grants.
        var read = authorization.Evaluate(Request, PermissionNames.ReadMentorTasks);
        if (read.Status != AuthorizationStatus.Authorized) return AuthorizationResponses.Denied(read.Status);
        try
        {
            var id = await tasks.CreateAsync(access.User!.Id, request, ct);
            return id is null ? AuthorizationResponses.Denied(AuthorizationStatus.Forbidden)
                : CreatedAtAction(nameof(Get), new { id }, new { id, message = "Đã giao nhiệm vụ." });
        }
        catch (ArgumentException error) { return BadRequest(new { message = error.Message }); }
    }

    [HttpPatch("{id:int}/progress")]
    public async Task<IActionResult> UpdateProgress(int id, UpdateTaskProgressRequest request, CancellationToken ct)
    {
        var access = Access();
        if (access.Status != AuthorizationStatus.Authorized) return AuthorizationResponses.Denied(access.Status);
        if (access.User!.Role != "INTERN") return AuthorizationResponses.Denied(AuthorizationStatus.Forbidden);
        access = authorization.Evaluate(Request, PermissionNames.UpdateOwnTasks);
        if (access.Status != AuthorizationStatus.Authorized) return AuthorizationResponses.Denied(access.Status);
        try
        {
            return await tasks.UpdateProgressAsync(access.User!.Id, id, request, ct)
                ? Ok(new { message = "Đã cập nhật tiến độ." })
                : AuthorizationResponses.Denied(AuthorizationStatus.Forbidden);
        }
        catch (ArgumentException error) { return BadRequest(new { message = error.Message }); }
    }
}
