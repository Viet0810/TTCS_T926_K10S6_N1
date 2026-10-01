namespace InternManagement.Models;

public sealed class AppUser
{
    public int Id { get; set; }
    public required string FullName { get; set; }
    public required string Email { get; set; }
    public required string UserName { get; set; }
    public required string PasswordHash { get; set; }
    public required string Role { get; set; }
}
