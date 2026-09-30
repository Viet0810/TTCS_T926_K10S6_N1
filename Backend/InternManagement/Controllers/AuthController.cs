using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using InternManagement.DTOs;
using InternManagement.Models;
using InternManagement.Services;

namespace InternManagement.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly string connectionString;
    private readonly PasswordHasher passwords;
    private readonly AuthTokenService tokens;
    private readonly RequestAuthorizationService authorization;
    private readonly RolePermissionService rolePermissions;

    public AuthController(
        IConfiguration configuration,
        PasswordHasher passwords,
        AuthTokenService tokens,
        RequestAuthorizationService authorization,
        RolePermissionService rolePermissions)
    {
        connectionString = configuration.GetConnectionString("InternManagement")
            ?? throw new InvalidOperationException("Chưa cấu hình connection string InternManagement.");
        this.passwords = passwords;
        this.tokens = tokens;
        this.authorization = authorization;
        this.rolePermissions = rolePermissions;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.Password))
            return BadRequest(new { message = "Vui lòng nhập tên đăng nhập và mật khẩu." });

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Username, FullName, Email, PasswordHash, Role
            FROM dbo.Users
            WHERE Username = @identifier OR Email = @identifier;
            """;
        command.Parameters.AddWithValue("@identifier", request.Username.Trim());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return Unauthorized(new { message = "Tên đăng nhập hoặc mật khẩu không đúng." });

        var user = new User
        {
            Id = reader.GetInt32(0),
            Username = reader.GetString(1),
            FullName = reader.GetString(2),
            Email = reader.GetString(3),
            PasswordHash = reader.GetString(4),
            Role = reader.GetString(5)
        };

        if (!passwords.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Tên đăng nhập hoặc mật khẩu không đúng." });

        var responseUser = new UserResponse(user.Id, user.Username, user.FullName, user.Email, user.Role);
        return Ok(new LoginResponse(tokens.Issue(user), responseUser));
    }

    [HttpGet("me")]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CurrentUserResponse>> GetCurrentUser(CancellationToken cancellationToken)
    {
        var decision = authorization.Evaluate(Request);
        if (decision.Status == AuthorizationStatus.Unauthenticated)
            return Unauthorized(new ApiErrorResponse(false, "Vui lòng đăng nhập để tiếp tục.", null));

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Username, FullName, Email, Role FROM dbo.Users WHERE Id = @id";
        command.Parameters.AddWithValue("@id", decision.User!.Id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return Unauthorized(new ApiErrorResponse(false, "Phiên đăng nhập không còn hợp lệ.", null));

        var user = new UserResponse(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4));

        return Ok(new CurrentUserResponse(user, rolePermissions.GetPermissions(user.Role)));
    }

    [HttpGet("roles/permissions")]
    [ProducesResponseType<IReadOnlyDictionary<string, IReadOnlyList<string>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public IActionResult GetRolePermissions()
    {
        var decision = authorization.Evaluate(Request, PermissionNames.ManagePermissions);
        if (decision.Status == AuthorizationStatus.Unauthenticated)
            return Unauthorized(new ApiErrorResponse(false, "Vui lòng đăng nhập để tiếp tục.", null));
        if (decision.Status == AuthorizationStatus.Forbidden)
            return StatusCode(StatusCodes.Status403Forbidden,
                new ApiErrorResponse(false, "Bạn không có quyền thực hiện chức năng này.", null));

        return Ok(rolePermissions.GetRolePermissions());
    }
}
