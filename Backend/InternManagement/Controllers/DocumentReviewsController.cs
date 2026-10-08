using InternManagement.DTOs;
using InternManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace InternManagement.Controllers;

[ApiController]
[Route("api/document-reviews")]
public sealed class DocumentReviewsController(InternDocumentService documents, ReviewEmailService reviewEmail,
    NotificationService notifications, ILogger<DocumentReviewsController> logger,
    RequestAuthorizationService authorization) : ControllerBase
{
    private AuthorizationDecision Access() => authorization.Evaluate(Request, PermissionNames.ApproveDocuments);

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
            return BadRequest(new { message = "Vui lòng nhập lý do yêu cầu bổ sung." });
        if (!TryVersion(request.Version, out var version))
            return BadRequest(new { message = "Phiên bản tài liệu không hợp lệ." });

        var result = await documents.ReviewAsync(id, kind, decision.User!.Id, request, version, cancellationToken);
        if (result == DocumentReviewResult.Updated)
        {
            var item = (await documents.ListReviewsAsync(cancellationToken))
                .FirstOrDefault(document => document.InternId == id && document.Kind == kind);
            if (item is null) return Ok(new { message = "Đã lưu kết quả duyệt.", notification = (ReviewNotificationResult?)null });
            var notification = await reviewEmail.SendAsync(item.Email, item.FullName, kind, request.Status,
                request.Comment, cancellationToken);
            await documents.SaveNotificationResultAsync(id, kind, request.Status, notification, CancellationToken.None);
            var documentName = kind == "cv" ? "CV" : "đơn xin thực tập";
            var internMessage = request.Status == "approved"
                ? $"HR đã duyệt {documentName} của bạn."
                : $"HR yêu cầu bổ sung {documentName}. Lý do: {request.Comment?.Trim()}";
            try
            {
                await notifications.NotifyInternAsync(item.Email, "Kết quả duyệt hồ sơ", internMessage,
                    "intern-upload-cv.html", CancellationToken.None);
            }
            catch (Exception error)
            {
                logger.LogWarning(error, "Could not create intern notification after document review.");
            }
            return Ok(new { message = "Đã lưu kết quả duyệt.", notification });
        }

        return result switch
        {
            DocumentReviewResult.NotFound => NotFound(new { message = "Không tìm thấy tài liệu. Vui lòng tải lại danh sách." }),
            DocumentReviewResult.AlreadyReviewed => Conflict(new { message = "Tài liệu đã có kết quả này. Vui lòng tải lại danh sách." }),
            _ => Conflict(new { message = "Tài liệu đã thay đổi. Vui lòng tải lại trước khi duyệt." })
        };
    }

    [HttpPost("{id:int}/{kind}/resend-email")]
    public async Task<IActionResult> ResendEmail(int id, string kind, CancellationToken cancellationToken)
    {
        var decision = Access();
        if (decision.Status != AuthorizationStatus.Authorized) return Denied(decision);
        if (!InternDocumentService.ValidKind(kind)) return BadRequest(new { message = "Loại tài liệu không hợp lệ." });

        var item = (await documents.ListReviewsAsync(cancellationToken))
            .FirstOrDefault(document => document.InternId == id && document.Kind == kind);
        if (item is null) return NotFound(new { message = "Không tìm thấy hồ sơ." });
        if (item.Status == "pending") return Conflict(new { message = "Chỉ có thể gửi email sau khi HR đã duyệt hoặc yêu cầu bổ sung." });

        var result = await reviewEmail.SendAsync(item.Email, item.FullName, kind, item.Status,
            item.Comment, cancellationToken);
        await documents.SaveNotificationResultAsync(id, kind, item.Status, result, CancellationToken.None);
        return Ok(new { notification = result });
    }

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
