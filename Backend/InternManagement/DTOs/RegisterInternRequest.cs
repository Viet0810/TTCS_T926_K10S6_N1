using System.ComponentModel.DataAnnotations;

namespace InternManagement.DTOs;

public sealed record RegisterInternRequest
{
    [Required, StringLength(200)] public required string FullName { get; init; }
    [Required, EmailAddress, StringLength(100)] public required string Email { get; init; }
    [Required, StringLength(200, MinimumLength = 8)] public required string Password { get; init; }
    [Required, RegularExpression(@"^[0-9]{10,11}$")] public required string Phone { get; init; }
    [Required, StringLength(200)] public required string School { get; init; }
    [Required, StringLength(200)] public required string Major { get; init; }
}
