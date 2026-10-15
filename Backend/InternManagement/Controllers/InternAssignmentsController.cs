using InternManagement.DTOs;
using InternManagement.Infrastructure;
using InternManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace InternManagement.Controllers;

[ApiController]
[Route("api/intern-assignments")]
public sealed class InternAssignmentsController(
    InternAssignmentService assignments, ProgramScheduleService programs,
    RequestAuthorizationService authorization, RolePermissionService permissions) : ControllerBase
{
    private IActionResult? Denied()
    {
        var decision = authorization.Evaluate(Request, PermissionNames.AssignMentor);
        if (decision.Status != AuthorizationStatus.Authorized) return AuthorizationResponses.Denied(decision.Status);
        return null;
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
        var access=authorization.Evaluate(Request);
        if(access.Status!=AuthorizationStatus.Authorized)return AuthorizationResponses.Denied(access.Status);
        var create=permissions.HasPermission(access.User!.Role,PermissionNames.CreateAssignment);
        var update=permissions.HasPermission(access.User.Role,PermissionNames.UpdateAssignment);
        if(!create && !update)return AuthorizationResponses.Denied(AuthorizationStatus.Forbidden);
        try
        {
            await assignments.AssignAsync(internId, request, cancellationToken,create,update);
            return Ok(new { success = true, message = "Đã lưu phân công Mentor." });
        }
        catch (AssignmentValidationException error)
        {
            var response = new ApiErrorResponse(false, error.Message, null);
            return error.NotFound ? NotFound(response) : BadRequest(response);
        }
        catch(UnauthorizedAccessException) { return AuthorizationResponses.Denied(AuthorizationStatus.Forbidden); }
    }
    [HttpDelete("{internId:int}")]
    public async Task<IActionResult> Delete(int internId,CancellationToken ct)
    {
        var access=authorization.Evaluate(Request,PermissionNames.DeleteAssignment);
        if(access.Status!=AuthorizationStatus.Authorized)return AuthorizationResponses.Denied(access.Status);
        return await assignments.DeleteAsync(internId,ct)?Ok(new{message="Đã xóa phân công."}):NotFound(new{message="Không tìm thấy phân công."});
    }
}
