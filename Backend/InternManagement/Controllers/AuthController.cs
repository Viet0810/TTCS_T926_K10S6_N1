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

    public AuthController(IConfiguration configuration, PasswordHasher passwords, AuthTokenService tokens)
    {
        connectionString = configuration.GetConnectionString("InternManagement")
            ?? throw new InvalidOperationException("Chưa cấu hình connection string InternManagement.");
        this.passwords = passwords;
        this.tokens = tokens;
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

    [HttpGet("permissions")]
    public IActionResult GetPermissions()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            || !tokens.TryValidate(header[7..].Trim(), out var user))
            return Unauthorized(new { message = "Phiên đăng nhập không hợp lệ hoặc đã hết hạn." });

        var role = user!.Role.ToUpperInvariant();
        var permissions = role switch
        {
            "ADMIN" => new[] { "users.manage", "interns.manage" },
            "HR" => new[] { "interns.manage" },
            "MENTOR" => new[] { "interns.assigned.read", "progress.review" },
            "INTERN" => new[] { "profile.own.read", "progress.own.read", "tasks.own.read" },
            _ => Array.Empty<string>()
        };

        return Ok(new { role, permissions });
    }
}
