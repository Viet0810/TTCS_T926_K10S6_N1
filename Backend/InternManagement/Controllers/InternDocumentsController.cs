using InternManagement.DTOs;
using InternManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace InternManagement.Controllers;

[ApiController]
[Route("api/interns/me/documents")]
public sealed class InternDocumentsController(InternDocumentService documents, NotificationService notifications,
    RequestAuthorizationService authorization, ILogger<InternDocumentsController> logger) : ControllerBase
{
    private async Task<int?> OwnProfile(CancellationToken cancellationToken)
    {
        var decision = authorization.Evaluate(Request, PermissionNames.UploadDocuments);
        if (decision.Status != AuthorizationStatus.Authorized)
        {
            Response.StatusCode = decision.Status == AuthorizationStatus.Unauthenticated ? 401 : 403;
            return null;
        }
        var ownerId = await documents.GetOwnerIdAsync(decision.User!.Id, cancellationToken);
        if (ownerId is null) Response.StatusCode = 404;
        return ownerId;
    }

    private IActionResult Denied() => StatusCode(Response.StatusCode, new ApiErrorResponse(false,
        Response.StatusCode == 404
            ? "Chưa có hồ sơ gắn với email tài khoản. Vui lòng liên hệ HR."
            : "Bạn cần đăng nhập bằng tài khoản thực tập sinh để thao tác tài liệu.", null));

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var ownerId = await OwnProfile(cancellationToken);
        if (ownerId is null) return Denied();
        return Ok(await documents.ListOwnedAsync(ownerId.Value, cancellationToken));
    }

    [HttpPut("{kind}")]
    [RequestSizeLimit(InternDocumentService.MaxBytes + 65536)]
    [RequestFormLimits(MultipartBodyLengthLimit = InternDocumentService.MaxBytes + 65536)]
    public async Task<IActionResult> Upload(string kind, [FromForm] IFormFile? file, CancellationToken cancellationToken)
    {
        var ownerId = await OwnProfile(cancellationToken);
        if (ownerId is null) return Denied();
        var validation = await documents.UploadAsync(ownerId.Value, kind, file, cancellationToken);
        if (validation is null)
        {
            try { await notifications.NotifyReviewersOfUploadAsync(ownerId.Value, kind, cancellationToken); }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                logger.LogWarning(error, "Could not create HR notification for an uploaded intern document.");
            }
        }
        return validation is null
            ? Ok(new { message = "Đã lưu tài liệu vào hệ thống." })
            : BadRequest(new { message = validation });
    }

    [HttpGet("{kind}")]
    public async Task<IActionResult> Download(string kind, CancellationToken cancellationToken)
    {
        var ownerId = await OwnProfile(cancellationToken);
        if (ownerId is null) return Denied();
        if (!InternDocumentService.ValidKind(kind)) return BadRequest(new { message = "Loại tài liệu không hợp lệ." });
        var document = await documents.DownloadAsync(ownerId.Value, kind, cancellationToken);
        if (document is null) return NotFound(new { message = "Chưa có tài liệu này." });
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.CacheControl = "no-store";
        return File(document.Content, "application/pdf", document.FileName);
    }
}
