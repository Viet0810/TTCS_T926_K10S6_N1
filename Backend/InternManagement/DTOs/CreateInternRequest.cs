using System.ComponentModel.DataAnnotations;

namespace InternManagement.DTOs;

/// <summary>Thông tin hồ sơ thực tập sinh cần tạo.</summary>
public sealed record CreateInternRequest
{
    [Required, StringLength(200)]
    public required string FullName { get; init; }

    [Required, EmailAddress, StringLength(254)]
    public required string Email { get; init; }

    [Required, RegularExpression(@"^[0-9]{10,11}$"), StringLength(20)]
    public required string Phone { get; init; }

    [Required, StringLength(200)]
    public required string School { get; init; }

    [Required, StringLength(200)]
    public required string Major { get; init; }
}