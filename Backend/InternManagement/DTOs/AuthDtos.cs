namespace InternManagement.DTOs;

public sealed record LoginRequest(string? Username, string? Password);

public sealed record UserResponse(int Id, string Username, string FullName, string Email, string Role, bool MustChangePassword = false);

public sealed record LoginResponse(string Token, UserResponse User);

public sealed record CurrentUserResponse(UserResponse User, IReadOnlyList<string> Permissions);
