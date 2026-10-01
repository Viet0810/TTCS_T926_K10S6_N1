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

    public UsersController(AccountService accounts, RequestAuthorizationService authorization)
    {
        this.accounts = accounts;
        this.authorization = authorization;
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

        var role = request.Role.Trim().ToUpperInvariant();
        if (role is not ("HR" or "MENTOR" or "INTERN"))
            return BadRequest(new ApiErrorResponse(false, "Vai trò không hợp lệ.", null));

        try
        {
            var user = await accounts.CreateAsync(request, role, cancellationToken);
            return CreatedAtAction(nameof(GetUserById), new { id = user.Id },
                new { success = true, message = "Tạo tài khoản thành công.", data = user });
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

}
