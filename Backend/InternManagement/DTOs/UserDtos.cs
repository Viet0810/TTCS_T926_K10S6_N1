using System.ComponentModel.DataAnnotations;

namespace InternManagement.DTOs;

public sealed record CreateUserRequest : IValidatableObject
{
	[Required, StringLength(200)]
	public required string FullName { get; init; }

	[Required, EmailAddress, StringLength(254)]
	public required string Email { get; init; }

    [StringLength(200)]
	public string? Password { get; init; }

	[Required, StringLength(10)]
	public required string Role { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.Equals(Role?.Trim(), "INTERN", StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrEmpty(Password) || Password.Length > 200 || !new StrongPasswordAttribute().IsValid(Password)))
            yield return new ValidationResult("Mật khẩu phải có ít nhất 8 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt.", [nameof(Password)]);
    }
}
