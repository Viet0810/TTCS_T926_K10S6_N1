using System.ComponentModel.DataAnnotations;

namespace InternManagement.DTOs;

/// <summary>Thông tin cần thiết để tạo tài khoản Mentor.</summary>
public sealed record CreateMentorRequest
{
    private string fullName = string.Empty;
    private string email = string.Empty;

    [Required, StringLength(200)]
    public required string FullName
    {
        get => fullName;
        init => fullName = value?.Trim() ?? string.Empty;
    }

    [Required, EmailAddress, StringLength(254)]
    public required string Email
    {
        get => email;
        init => email = value?.Trim() ?? string.Empty;
    }
}
