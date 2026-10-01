using System.ComponentModel.DataAnnotations;

namespace InternManagement.DTOs;

public sealed record CreateUserRequest
{
	[Required, StringLength(200)]
	public required string FullName { get; init; }

	[Required, EmailAddress, StringLength(254)]
	public required string Email { get; init; }

	[Required, StringLength(200, MinimumLength = 8)]
	public required string Password { get; init; }

	[Required, StringLength(10)]
	public required string Role { get; init; }
}
