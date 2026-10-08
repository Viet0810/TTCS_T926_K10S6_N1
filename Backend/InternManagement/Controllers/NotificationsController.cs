using InternManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace InternManagement.Controllers;

[ApiController]
[Route("api/notifications")]
public sealed class NotificationsController(NotificationService notifications, RequestAuthorizationService authorization) : ControllerBase
{
    private int? CurrentUserId()
    {
        var decision = authorization.Evaluate(Request);
        if (decision.Status == AuthorizationStatus.Authorized) return decision.User!.Id;
        Response.StatusCode = decision.Status == AuthorizationStatus.Unauthenticated ? 401 : 403;
        return null;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        return userId is null ? StatusCode(Response.StatusCode, new { message = "Vui lòng đăng nhập để xem thông báo." })
            : Ok(await notifications.ListForUserAsync(userId.Value, cancellationToken));
    }

    [HttpPut("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        if (userId is null) return StatusCode(Response.StatusCode, new { message = "Vui lòng đăng nhập để cập nhật thông báo." });
        return await notifications.MarkReadAsync(id, userId.Value, cancellationToken)
            ? Ok(new { updated = true })
            : NotFound(new { message = "Không tìm thấy thông báo." });
    }

    [HttpDelete]
    public async Task<IActionResult> Clear(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        return userId is null ? StatusCode(Response.StatusCode, new { message = "Vui lòng đăng nhập để xóa thông báo." })
            : Ok(new { deleted = await notifications.ClearForUserAsync(userId.Value, cancellationToken) });
    }
}
