using System.ComponentModel.DataAnnotations;

namespace InternManagement.DTOs;

public sealed record InternDocumentResponse(string Kind, string FileName, long Size, DateTime UploadedAt,
    string Status, string? Comment);

public sealed record DocumentReviewResponse(int InternId, string FullName, string? StudentCode, string Email,
    string Kind, string FileName, long Size, DateTime UploadedAt, string Status, string? Comment,
    DateTime? ReviewedAt, string? Reviewer, string Version);

public sealed record ReviewDocumentRequest(
    [Required(ErrorMessage = "Vui lòng chọn kết quả duyệt.")]
    [RegularExpression("^(approved|rejected)$", ErrorMessage = "Trạng thái duyệt không hợp lệ.")] string Status,
    [StringLength(2000, ErrorMessage = "Nhận xét không được vượt quá 2000 ký tự.")] string? Comment,
    [Required(ErrorMessage = "Thiếu phiên bản tài liệu. Vui lòng tải lại danh sách.")] string Version);

public sealed record StoredDocument(string FileName, byte[] Content);
