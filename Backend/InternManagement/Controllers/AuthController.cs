using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using InternManagement.DTOs;
using InternManagement.Infrastructure;
using InternManagement.Services;
using Microsoft.AspNetCore.RateLimiting;

namespace InternManagement.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly AccountService accounts;
    private readonly AuthTokenService tokens;
    private readonly RequestAuthorizationService authorization;
    private readonly RolePermissionService rolePermissions;
    private readonly PasswordResetService passwordReset;
    private readonly ILogger<AuthController> logger;

    public AuthController(
        AccountService accounts,
        AuthTokenService tokens,
        RequestAuthorizationService authorization,
        RolePermissionService rolePermissions,
        PasswordResetService passwordReset,
        ILogger<AuthController> logger)
    {
        this.accounts = accounts;
        this.tokens = tokens;
        this.authorization = authorization;
        this.rolePermissions = rolePermissions;
        this.passwordReset = passwordReset;
        this.logger = logger;
    }

    [HttpPost("register")]
    public async Task<ActionResult<UserResponse>> Register(RegisterInternRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await accounts.RegisterInternAsync(request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, user);
        }
        catch (SqlException error) when (error.Number is 2601 or 2627)
        {
            return Conflict(new { message = "Email đã được sử dụng cho tài khoản hoặc hồ sơ. Vui lòng đăng nhập hoặc liên hệ HR." });
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.Password))
            return BadRequest(new { message = "Vui lòng nhập tên đăng nhập và mật khẩu." });

        var user = await accounts.AuthenticateAsync(request, cancellationToken);
        if (user is null)
            return Unauthorized(new { message = "Tên đăng nhập hoặc mật khẩu không đúng." });

        var responseUser = new UserResponse(user.Id, user.Username, user.FullName, user.Email, user.Role);
        return Ok(new LoginResponse(tokens.Issue(user), responseUser));
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting("password-recovery")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await passwordReset.RequestAsync(request.Email, cancellationToken);
            return Ok(new { message = "Nếu email đã được đăng ký, bạn sẽ nhận được liên kết đặt lại mật khẩu. Vui lòng kiểm tra hộp thư và thư rác." });
        }
        catch (PasswordRecoveryUnavailableException error)
        {
            logger.LogWarning("Password recovery unavailable. Trace {TraceId}", HttpContext.TraceIdentifier);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = error.Message });
        }
        catch (SqlException error)
        {
            logger.LogError(error, "Password recovery database operation failed. Trace {TraceId}", HttpContext.TraceIdentifier);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Không thể kết nối cơ sở dữ liệu. Vui lòng thử lại sau." });
        }
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting("password-recovery")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await passwordReset.ResetAsync(request.Token, request.Password, cancellationToken)
                ? Ok(new { message = "Đã đặt lại mật khẩu. Bạn có thể đăng nhập bằng mật khẩu mới." })
                : BadRequest(new { message = "Liên kết đã hết hạn hoặc đã được sử dụng. Vui lòng gửi lại yêu cầu quên mật khẩu." });
        }
        catch (SqlException error)
        {
            logger.LogError(error, "Password reset database operation failed. Trace {TraceId}", HttpContext.TraceIdentifier);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Không thể cập nhật mật khẩu. Vui lòng thử lại sau." });
        }
    }

    [HttpGet("me")]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CurrentUserResponse>> GetCurrentUser(CancellationToken cancellationToken)
    {
        var decision = authorization.Evaluate(Request);
        if (decision.Status == AuthorizationStatus.Unauthenticated)
            return Unauthorized(new ApiErrorResponse(false, "Vui lòng đăng nhập để tiếp tục.", null));

        var user = await accounts.GetByIdAsync(decision.User!.Id, cancellationToken);
        if (user is null)
            return Unauthorized(new ApiErrorResponse(false, "Phiên đăng nhập không còn hợp lệ.", null));

        return Ok(new CurrentUserResponse(user, rolePermissions.GetPermissions(user.Role)));
    }

    [HttpGet("roles/permissions")]
    [ProducesResponseType<IReadOnlyDictionary<string, IReadOnlyList<string>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public IActionResult GetRolePermissions()
    {
        var decision = authorization.Evaluate(Request, PermissionNames.ManagePermissions);
        if (decision.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(decision.Status);

        return Ok(rolePermissions.GetRolePermissions());
    }
}
