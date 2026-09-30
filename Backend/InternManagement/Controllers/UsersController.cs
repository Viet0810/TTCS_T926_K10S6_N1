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
    private readonly RequestAuthorizationService authorization;

    public UsersController(IConfiguration configuration, PasswordHasher passwords, RequestAuthorizationService authorization)
    {
        connectionString = configuration.GetConnectionString("InternManagement")
            ?? throw new InvalidOperationException("Chưa cấu hình connection string InternManagement.");
        this.passwords = passwords;
        this.authorization = authorization;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken)
    {
        var decision = authorization.Evaluate(Request, PermissionNames.ManageUsers);
        if (decision.Status != AuthorizationStatus.Authorized)
            return AccessDenied(decision.Status);

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

    [HttpGet("{id:int}")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserById(int id, CancellationToken cancellationToken)
    {
        var decision = authorization.Evaluate(Request, PermissionNames.ManageUsers);
        if (decision.Status != AuthorizationStatus.Authorized)
            return AccessDenied(decision.Status);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Username, FullName, Email, Role FROM dbo.Users WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return NotFound(new ApiErrorResponse(false, "Không tìm thấy tài khoản.", null));

        return Ok(new UserResponse(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4)));
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var decision = authorization.Evaluate(Request, PermissionNames.CreateUser);
        if (decision.Status != AuthorizationStatus.Authorized)
            return AccessDenied(decision.Status);

        var role = request.Role.Trim().ToUpperInvariant();
        if (role is not ("HR" or "MENTOR" or "INTERN"))
            return BadRequest(new ApiErrorResponse(false, "Vai trò không hợp lệ.", null));

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO dbo.Users (Username, FullName, Email, PasswordHash, Role)
                OUTPUT INSERTED.Id, INSERTED.Username, INSERTED.FullName, INSERTED.Email, INSERTED.Role
                VALUES (@username, @fullName, @email, @passwordHash, @role);
                """;
            command.Parameters.AddWithValue("@username", request.Email.Trim());
            command.Parameters.AddWithValue("@fullName", request.FullName.Trim());
            command.Parameters.AddWithValue("@email", request.Email.Trim());
            command.Parameters.AddWithValue("@passwordHash", passwords.Hash(request.Password));
            command.Parameters.AddWithValue("@role", role);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            var user = new UserResponse(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4));
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
            return AccessDenied(decision.Status);
        if (decision.User!.Id == id)
            return BadRequest(new ApiErrorResponse(false, "Không thể xóa tài khoản đang đăng nhập.", null));

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM dbo.Users WHERE Id = @id AND Role <> 'ADMIN'";
        command.Parameters.AddWithValue("@id", id);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        return affected == 0
            ? NotFound(new ApiErrorResponse(false, "Không tìm thấy tài khoản cần xóa.", null))
            : NoContent();
    }

    private IActionResult AccessDenied(AuthorizationStatus status) => status switch
    {
        AuthorizationStatus.Unauthenticated => Unauthorized(new ApiErrorResponse(false, "Vui lòng đăng nhập để tiếp tục.", null)),
        _ => StatusCode(StatusCodes.Status403Forbidden, new ApiErrorResponse(false, "Bạn không có quyền thực hiện chức năng này.", null))
    };
}
