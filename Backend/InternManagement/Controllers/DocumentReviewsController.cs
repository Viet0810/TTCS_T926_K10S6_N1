using InternManagement.DTOs;
using InternManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace InternManagement.Controllers;

[ApiController]
[Route("api/document-reviews")]
public sealed class DocumentReviewsController(InternDocumentService documents, RequestAuthorizationService authorization) : ControllerBase
{
    private AuthorizationDecision Access()
    {
        var decision = authorization.Evaluate(Request, PermissionNames.ApproveDocuments);
        return decision.Status == AuthorizationStatus.Authorized && decision.User!.Role != "HR"
            ? new AuthorizationDecision(AuthorizationStatus.Forbidden, decision.User) : decision;
    }

    private IActionResult Denied(AuthorizationDecision decision) => StatusCode(
        decision.Status == AuthorizationStatus.Unauthenticated ? 401 : 403,
        new { message = "Bạn cần quyền duyệt tài liệu để truy cập." });

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var decision = Access();
        if (decision.Status != AuthorizationStatus.Authorized) return Denied(decision);
        return Ok(await documents.ListReviewsAsync(cancellationToken));
    }

    [HttpGet("{id:int}/{kind}")]
    public async Task<IActionResult> Download(int id, string kind, CancellationToken cancellationToken)
    {
        var decision = Access();
        if (decision.Status != AuthorizationStatus.Authorized) return Denied(decision);
        if (!InternDocumentService.ValidKind(kind)) return BadRequest(new { message = "Loại tài liệu không hợp lệ." });
        var document = await documents.DownloadAsync(id, kind, cancellationToken);
        if (document is null) return NotFound(new { message = "Không tìm thấy tài liệu." });
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(document.Content, "application/pdf", document.FileName);
    }

    [HttpPut("{id:int}/{kind}")]
    public async Task<IActionResult> Review(int id, string kind, ReviewDocumentRequest request, CancellationToken cancellationToken)
    {
        var decision = Access();
        if (decision.Status != AuthorizationStatus.Authorized) return Denied(decision);
        if (!InternDocumentService.ValidKind(kind)) return BadRequest(new { message = "Loại tài liệu không hợp lệ." });
        if (request.Status == "rejected" && string.IsNullOrWhiteSpace(request.Comment))
            return BadRequest(new { message = "Vui lòng nhập lý do từ chối." });
        if (!TryVersion(request.Version, out var version))
            return BadRequest(new { message = "Phiên bản tài liệu không hợp lệ." });

        var result = await documents.ReviewAsync(id, kind, decision.User!.Id, request, version, cancellationToken);
        return result switch
        {
            DocumentReviewResult.Updated => Ok(new { message = "Đã lưu kết quả duyệt." }),
            DocumentReviewResult.NotFound => NotFound(new { message = "Không tìm thấy tài liệu. Vui lòng tải lại danh sách." }),
            DocumentReviewResult.AlreadyReviewed => Conflict(new { message = "Tài liệu đã được xử lý, không thể xét duyệt lại." }),
            _ => Conflict(new { message = "Tài liệu đã thay đổi. Vui lòng tải lại trước khi duyệt." })
        };
    }

    [HttpPut("{id:int}/{kind}/approve")]
    public Task<IActionResult> Approve(int id, string kind, DocumentDecisionRequest request, CancellationToken cancellationToken)
        => Review(id, kind, new ReviewDocumentRequest("approved", request.Comment, request.Version), cancellationToken);

    [HttpPut("{id:int}/{kind}/reject")]
    public Task<IActionResult> Reject(int id, string kind, DocumentDecisionRequest request, CancellationToken cancellationToken)
        => Review(id, kind, new ReviewDocumentRequest("rejected", request.Comment, request.Version), cancellationToken);

    private static bool TryVersion(string value, out byte[] version)
    {
        try
        {
            version = Convert.FromBase64String(value);
            return version.Length == 8;
        }
        catch (FormatException)
        {
            version = [];
            return false;
        }
    }
}
