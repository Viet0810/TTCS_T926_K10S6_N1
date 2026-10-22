using InternManagement.DTOs;
using InternManagement.Infrastructure;
using InternManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace InternManagement.Controllers;

[ApiController]
[Route("api/admin/demo-accounts")]
public sealed class AdminDemoAccountsController(
    DemoAccountService demoAccounts,
    RequestAuthorizationService authorization) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAccounts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var denied = DeniedUnlessAdmin();
        if (denied is not null) return denied;
        return Ok(await demoAccounts.GetAccountsAsync(page, pageSize, search, cancellationToken));
    }

    [HttpPost("{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, CancellationToken cancellationToken)
    {
        var denied = DeniedUnlessAdmin();
        if (denied is not null) return denied;

        return await demoAccounts.ResetPasswordAsync(id, cancellationToken)
            ? Ok(new { message = "Đã đặt lại mật khẩu demo theo vai trò." })
            : NotFound(new ApiErrorResponse(false, "Không tìm thấy tài khoản demo.", null));
    }

    private IActionResult? DeniedUnlessAdmin()
    {
        var decision = authorization.Evaluate(Request);
        if (decision.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(decision.Status);
        return decision.User!.Role == "ADMIN"
            ? null
            : AuthorizationResponses.Denied(AuthorizationStatus.Forbidden);
    }
}
