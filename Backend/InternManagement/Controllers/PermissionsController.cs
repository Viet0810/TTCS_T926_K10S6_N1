using System.ComponentModel.DataAnnotations;
using InternManagement.Services;
using InternManagement.Infrastructure;
using Microsoft.AspNetCore.Mvc;
namespace InternManagement.Controllers;

public sealed record SavePermissionsRequest([Required, MaxLength(64)] string[] Permissions);

[ApiController]
[Route("api/permissions")]
public sealed class PermissionsController(RolePermissionService permissions, RequestAuthorizationService auth) : ControllerBase
{
    private IActionResult? Denied(string permission)
    {
        var decision = auth.Evaluate(Request, permission);
        if (decision.Status != AuthorizationStatus.Authorized) return AuthorizationResponses.Denied(decision.Status);
        return decision.User!.Role == "ADMIN" ? null : AuthorizationResponses.Denied(AuthorizationStatus.Forbidden);
    }

    [HttpGet]
    public IActionResult Get()
    {
        if (Denied(PermissionNames.ReadPermissions) is {} denied) return denied;
        return Ok(new { roles = permissions.GetRolePermissions(), catalog = permissions.GetCatalog(),
            essentialAdminPermissions = RolePermissionService.EssentialAdminPermissions });
    }

    [HttpGet("{role}")]
    public IActionResult Role(string role)
    {
        if(Denied(PermissionNames.ReadPermissions) is {} denied)return denied;
        role=role.ToUpperInvariant();
        return permissions.GetRolePermissions().ContainsKey(role)?Ok(new{role,permissions=permissions.GetPermissions(role)}):BadRequest(new{message="Vai trò không hợp lệ."});
    }

    [HttpGet("{role}/defaults")]
    public IActionResult Defaults(string role)
    {
        if (Denied(PermissionNames.ReadPermissions) is {} denied) return denied;
        role = role.ToUpperInvariant();
        return !permissions.GetRolePermissions().ContainsKey(role) ? BadRequest(new { message = "Vai trò không hợp lệ." })
            : Ok(new { permissions = permissions.GetDefaults(role) });
    }

    [HttpPut("{role}")]
    public async Task<IActionResult> Save(string role, SavePermissionsRequest request, CancellationToken ct)
    {
        if (Denied(PermissionNames.ManagePermissions) is {} denied) return denied;
        try
        {
            role = role.ToUpperInvariant();
            await permissions.SaveAsync(role, request.Permissions, ct);
            return Ok(new { permissions = permissions.GetPermissions(role), message = "Đã lưu phân quyền." });
        }
        catch (ArgumentException error) { return BadRequest(new { message = error.Message }); }
    }
}
