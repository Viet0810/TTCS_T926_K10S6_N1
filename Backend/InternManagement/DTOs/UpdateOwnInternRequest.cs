using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace InternManagement.DTOs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateOwnInternRequest
{
    private string fullName = "", phone = "", school = "", major = "";
    [Required(ErrorMessage = "Vui lòng nhập họ và tên."), StringLength(200)]
    public required string FullName { get => fullName; init => fullName = value?.Trim() ?? ""; }
    [Required(ErrorMessage = "Vui lòng nhập số điện thoại."), RegularExpression(@"^0[35789]\d{8}$", ErrorMessage = "Số điện thoại phải gồm 10 chữ số và đúng định dạng số di động Việt Nam.")]
    public required string Phone { get => phone; init => phone = value?.Trim() ?? ""; }
    [Required(ErrorMessage = "Vui lòng nhập trường."), StringLength(200)]
    public required string School { get => school; init => school = value?.Trim() ?? ""; }
    [Required(ErrorMessage = "Vui lòng nhập chuyên ngành."), StringLength(200)]
    public required string Major { get => major; init => major = value?.Trim() ?? ""; }
    [StringLength(500)]
    public string? Address { get; init; }
}
