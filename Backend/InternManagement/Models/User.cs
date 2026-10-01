namespace InternManagement.Models;

public sealed class User
{
    public int Id { get; init; }
    public string Username { get; init; } = "";
    public string FullName { get; init; } = "";
    public string Email { get; init; } = "";
    public string PasswordHash { get; init; } = "";
    public string Role { get; init; } = "";
}
