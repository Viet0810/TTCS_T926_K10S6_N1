using System.ComponentModel.DataAnnotations;

namespace InternManagement.DTOs;

public sealed record RegisterInternRequest
{
    [Required, StringLength(200)] public required string FullName { get; init; }
    [Required, EmailAddress, StringLength(100), RegularExpression(@"^[a-zA-Z0-9._%+-]+@(gmail\.com|ictu\.edu\.vn)$")]
    public required string Email { get; init; }
    [Required, StringLength(200, MinimumLength = 8), StrongPassword]
    public required string Password { get; init; }
    [Required, RegularExpression(@"^0[35789]\d{8}$")] public required string Phone { get; init; }
    [Required, StringLength(200)] public required string School { get; init; }
    [Required, StringLength(200)] public required string Major { get; init; }
}
