using System.ComponentModel.DataAnnotations;

namespace InternManagement.DTOs;

public sealed class StrongPasswordAttribute : RegularExpressionAttribute
{
    public StrongPasswordAttribute() : base(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,}$")
    {
        ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt.";
    }
}

public sealed record ChangePasswordRequest
{
    [Required, StringLength(200)]
    public required string CurrentPassword { get; init; }
    [Required, StringLength(200), StrongPassword]
    public required string NewPassword { get; init; }
    [Required, StringLength(200), Compare(nameof(NewPassword), ErrorMessage = "Xác nhận mật khẩu không khớp.")]
    public required string ConfirmPassword { get; init; }
}
