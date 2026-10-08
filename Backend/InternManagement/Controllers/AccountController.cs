using InternManagement.DTOs;
using InternManagement.Infrastructure;
using InternManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace InternManagement.Controllers;

[ApiController]
[Route("api/account")]
public sealed class AccountController(AccountService accounts, AuthTokenService tokens, RequestAuthorizationService authorization) : ControllerBase
{
    [HttpPut("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var decision = authorization.Evaluate(Request);
        if (decision.Status != AuthorizationStatus.Authorized) return AuthorizationResponses.Denied(decision.Status);
        if (request.NewPassword == request.CurrentPassword)
            return BadRequest(new { code = "SAME_PASSWORD", message = "Mật khẩu mới phải khác mật khẩu hiện tại." });
        if (!await accounts.ChangePasswordAsync(decision.User!.Id, request, cancellationToken))
            return BadRequest(new { code = "INVALID_CURRENT_PASSWORD", message = "Mật khẩu hiện tại không đúng." });
        var user = await accounts.AuthenticateAsync(new LoginRequest(decision.User.Username, request.NewPassword), cancellationToken);
        if (user is null) return Unauthorized(new { message = "Mật khẩu đã được cập nhật. Vui lòng đăng nhập lại." });
        return Ok(new { message = "Đổi mật khẩu thành công.", token = tokens.Issue(user),
            user = new UserResponse(user.Id, user.Username, user.FullName, user.Email, user.Role, user.MustChangePassword) });
    }
}
