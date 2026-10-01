using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using InternManagement.DTOs;
using InternManagement.Services;

namespace InternManagement.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private readonly string connectionString;
    private readonly PasswordHasher passwords;
    private readonly AuthTokenService tokens;

    public UsersController(IConfiguration configuration, PasswordHasher passwords, AuthTokenService tokens)
    {
        connectionString = configuration.GetConnectionString("InternManagement")
            ?? throw new InvalidOperationException("Chưa cấu hình connection string InternManagement.");
        this.passwords = passwords;
        this.tokens = tokens;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserResponse>>> GetUsers(CancellationToken cancellationToken)
    {
        var authorizationFailure = AuthorizeUserManagement(out _);
        if (authorizationFailure is not null) return authorizationFailure;

        var users = new List<UserResponse>();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Username, FullName, Email, Role FROM dbo.Users ORDER BY Id DESC";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            users.Add(new UserResponse(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4)));

        return Ok(users);
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var authorizationFailure = AuthorizeUserManagement(out _);
        if (authorizationFailure is not null) return authorizationFailure;
        var role = request.Role?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password))
            return BadRequest(new { message = "Vui lòng nhập đủ thông tin tài khoản." });
        if (request.Password.Length < 8)
            return BadRequest(new { message = "Mật khẩu phải có ít nhất 8 ký tự." });
        if (role is not ("HR" or "MENTOR" or "INTERN"))
            return BadRequest(new { message = "Vai trò không hợp lệ." });

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO dbo.Users (Username, FullName, Email, PasswordHash, Role)
                VALUES (@username, @fullName, @email, @passwordHash, @role);
                """;
            command.Parameters.AddWithValue("@username", request.Email.Trim());
            command.Parameters.AddWithValue("@fullName", request.FullName.Trim());
            command.Parameters.AddWithValue("@email", request.Email.Trim());
            command.Parameters.AddWithValue("@passwordHash", passwords.Hash(request.Password));
            command.Parameters.AddWithValue("@role", role);
            await command.ExecuteNonQueryAsync(cancellationToken);
            if (role == "INTERN")
            {
                await using var link = connection.CreateCommand();
                link.CommandText = "UPDATE dbo.Interns SET UserId=(SELECT Id FROM dbo.Users WHERE Email=@email AND Role='INTERN') WHERE Email=@email AND UserId IS NULL";
                link.Parameters.AddWithValue("@email", request.Email.Trim());
                await link.ExecuteNonQueryAsync(cancellationToken);
            }
            return Ok(new { message = "Tạo tài khoản thành công." });
        }
        catch (SqlException error) when (error.Number is 2601 or 2627)
        {
            return Conflict(new { message = "Email hoặc tên đăng nhập đã tồn tại." });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteUser(int id, CancellationToken cancellationToken)
    {
        var authorizationFailure = AuthorizeUserManagement(out var currentUser);
        if (authorizationFailure is not null) return authorizationFailure;
        if (currentUser!.Id == id) return BadRequest(new { message = "Không thể xóa tài khoản đang đăng nhập." });

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM dbo.Users WHERE Id = @id AND Role <> 'ADMIN'";
        command.Parameters.AddWithValue("@id", id);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        return affected == 0 ? NotFound(new { message = "Không tìm thấy tài khoản cần xóa." }) : Ok(new { message = "Đã xóa tài khoản." });
    }

    private ActionResult? AuthorizeUserManagement(out AuthenticatedUser? user)
    {
        user = null;
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            || !tokens.TryValidate(header[7..].Trim(), out user))
            return Unauthorized(new { message = "Bạn cần đăng nhập để thực hiện thao tác này." });

        return RolePermissions.HasPermission(user!.Role, "users.manage")
            ? null
            : StatusCode(StatusCodes.Status403Forbidden, new { message = "Vai trò hiện tại không có quyền quản lý tài khoản." });
    }

    [HttpPut("{id:int}/role")]
    public async Task<IActionResult> UpdateRole(int id, UpdateUserRoleRequest request, CancellationToken cancellationToken)
    {
        var authorizationFailure = AuthorizeUserManagement(out var currentUser);
        if (authorizationFailure is not null) return authorizationFailure;
        var role = request.Role?.Trim().ToUpperInvariant();
        if (role is not ("HR" or "MENTOR" or "INTERN"))
            return BadRequest(new { message = "Chỉ có thể phân quyền tài khoản thành HR, Mentor hoặc Intern." });
        if (currentUser!.Id == id)
            return BadRequest(new { message = "Không thể thay đổi vai trò của tài khoản Admin đang đăng nhập." });

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE dbo.Users SET Role=@role WHERE Id=@id AND Role<>'ADMIN'";
        command.Parameters.AddWithValue("@role", role); command.Parameters.AddWithValue("@id", id);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 0
            ? NotFound(new { message = "Không tìm thấy tài khoản có thể phân quyền." })
            : Ok(new { message = "Đã cập nhật vai trò. Người dùng cần đăng nhập lại để nhận quyền mới." });
    }
}

public sealed record UpdateUserRoleRequest(string? Role);
