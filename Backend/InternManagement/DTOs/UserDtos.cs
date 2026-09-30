namespace InternManagement.DTOs;

public sealed record CreateUserRequest(string? FullName, string? Email, string? Password, string? Role);
