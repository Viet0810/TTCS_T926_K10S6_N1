using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using InternManagement.DTOs;
using InternManagement.Services;
using InternManagement.Infrastructure;

namespace InternManagement.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private readonly AccountService accounts;
    private readonly RequestAuthorizationService authorization;
    private readonly PasswordResetService passwordReset;
    private readonly ILogger<UsersController> logger;

    public UsersController(AccountService accounts, RequestAuthorizationService authorization,
        PasswordResetService passwordReset, ILogger<UsersController> logger)
    {
        this.accounts = accounts;
        this.authorization = authorization;
        this.passwordReset = passwordReset;
        this.logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken)
    {
        var decision = authorization.Evaluate(Request, PermissionNames.ManageUsers);
        if (decision.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(decision.Status);

        var users = await accounts.GetAllAsync(cancellationToken);
        return Ok(users);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserById(int id, CancellationToken cancellationToken)
    {
        var decision = authorization.Evaluate(Request, PermissionNames.ManageUsers);
        if (decision.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(decision.Status);

        var user = await accounts.GetByIdAsync(id, cancellationToken);
        return user is null
            ? NotFound(new ApiErrorResponse(false, "Không tìm thấy tài khoản.", null))
            : Ok(user);
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var decision = authorization.Evaluate(Request, PermissionNames.CreateUser);
        if (decision.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(decision.Status);
        if (decision.User!.Role != "ADMIN") return AuthorizationResponses.Denied(AuthorizationStatus.Forbidden);

        var role = request.Role.Trim().ToUpperInvariant();
        if (role is not ("HR" or "MENTOR" or "INTERN"))
            return BadRequest(new ApiErrorResponse(false, "Vai trò không hợp lệ.", null));

        try
        {
            if (role is "HR" or "MENTOR") request = request with { Password = TemporaryPasswordGenerator.Generate() };
            var user = await accounts.CreateAsync(request, role, cancellationToken);
            logger.LogInformation("Account created. User {UserId}, recipient {Recipient}, role {Role}.", user.Id, user.Email, user.Role);
            bool? emailSent = null;
            var message = "Tạo tài khoản thành công.";
            if (role is "HR" or "MENTOR")
            {
                emailSent = false;
                try { emailSent = await passwordReset.SendAccountCreatedAsync(user, request.Password!, cancellationToken); }
                catch (Exception error)
                {
                    logger.LogWarning("Account setup email failed for user {UserId}, recipient {Recipient}, type {FailureType}, reason {Reason}.",
                        user.Id, user.Email, error.GetType().Name, error is PasswordRecoveryUnavailableException ? error.Message : "Delivery unavailable");
                }
                message = emailSent == true
                    ? "Tạo tài khoản thành công. Email hướng dẫn đã được gửi đến người dùng."
                    : "Tài khoản đã được tạo nhưng chưa gửi được email hướng dẫn.";
            }
            return CreatedAtAction(nameof(GetUserById), new { id = user.Id },
                new { success = true, message, data = user, accountCreated = true, emailSent });
        }
        catch (SqlException error) when (error.Number is 2601 or 2627)
        {
            return Conflict(new ApiErrorResponse(false, "Email hoặc tên đăng nhập đã tồn tại.", null));
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteUser(int id, CancellationToken cancellationToken)
    {
        var decision = authorization.Evaluate(Request, PermissionNames.DeleteUser);
        if (decision.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(decision.Status);
        if (decision.User!.Id == id)
            return BadRequest(new ApiErrorResponse(false, "Không thể xóa tài khoản đang đăng nhập.", null));

        var deleted = await accounts.DeleteAsync(id, cancellationToken);
        return !deleted
            ? NotFound(new ApiErrorResponse(false, "Không tìm thấy tài khoản cần xóa.", null))
            : NoContent();
    }

    [HttpPost("{id:int}/resend-login-email")]
    public async Task<IActionResult> ResendLoginEmail(int id, CancellationToken cancellationToken)
    {
        var decision = authorization.Evaluate(Request, PermissionNames.CreateUser);
        if (decision.Status != AuthorizationStatus.Authorized) return AuthorizationResponses.Denied(decision.Status);
        if (decision.User!.Role != "ADMIN") return AuthorizationResponses.Denied(AuthorizationStatus.Forbidden);
        var user = await accounts.GetByIdAsync(id, cancellationToken);
        if (user is null) return NotFound(new { message = "Không tìm thấy tài khoản." });
        if (user.Role is not ("HR" or "MENTOR")) return BadRequest(new { message = "Chỉ gửi lại email đăng nhập cho HR/Mentor." });
        var password = TemporaryPasswordGenerator.Generate();
        if (!await accounts.SetTemporaryPasswordAsync(id, password, cancellationToken))
            return NotFound(new { message = "Tài khoản không còn khả dụng." });
        var emailSent = false;
        try { emailSent = await passwordReset.SendAccountCreatedAsync(user, password, cancellationToken); }
        catch (Exception error) { logger.LogWarning("Account email resend failed for user {UserId}, recipient {Recipient}, type {FailureType}, reason {Reason}.",
            id, user.Email, error.GetType().Name, error is PasswordRecoveryUnavailableException ? error.Message : "Delivery unavailable"); }
        return Ok(new { emailSent, mustChangePassword = true, message = emailSent
            ? "Đã gửi email với mật khẩu tạm mới."
            : "Đã thay mật khẩu tạm nhưng chưa gửi được email. Vui lòng kiểm tra SMTP rồi gửi lại." });
    }

}
