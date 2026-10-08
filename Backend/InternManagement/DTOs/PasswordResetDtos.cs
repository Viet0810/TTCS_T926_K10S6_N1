using System.ComponentModel.DataAnnotations;

namespace InternManagement.DTOs;

public sealed record ForgotPasswordRequest
{
    [Required, EmailAddress, StringLength(254)]
    public required string Email { get; init; }
}

public sealed record ResetPasswordRequest
{
    [Required, RegularExpression("^[A-Fa-f0-9]{64}$")]
    public required string Token { get; init; }

    [Required, StringLength(200, MinimumLength = 8), StrongPassword]
    public required string Password { get; init; }
}
