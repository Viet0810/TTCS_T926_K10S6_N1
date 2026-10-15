using InternManagement.DTOs;
using InternManagement.Infrastructure;
using InternManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace InternManagement.Controllers;

[ApiController]
[Route("api/notifications")]
public sealed class NotificationsController(
    INotificationService notifications,
    RequestAuthorizationService auth
) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 15,
        [FromQuery] bool unreadOnly = false,
        [FromQuery] string? category = null,
        CancellationToken ct = default)
    {
        var access = auth.Evaluate(Request);
        if (access.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(access.Status);

        var result = await notifications.GetNotificationsAsync(
            access.User!.Id,
            page,
            pageSize,
            unreadOnly,
            category,
            ct);

        return Ok(result);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct = default)
    {
        var access = auth.Evaluate(Request);
        if (access.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(access.Status);

        var result = await notifications.GetUnreadCountAsync(access.User!.Id, ct);
        return Ok(result);
    }

    [HttpPatch("{id:long}/read")]
    public async Task<IActionResult> MarkAsReadPatch([FromRoute] long id, CancellationToken ct = default)
    {
        return await HandleMarkAsRead(id, ct);
    }

    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> MarkAsReadPost([FromRoute] long id, CancellationToken ct = default)
    {
        return await HandleMarkAsRead(id, ct);
    }

    private async Task<IActionResult> HandleMarkAsRead(long id, CancellationToken ct)
    {
        var access = auth.Evaluate(Request);
        if (access.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(access.Status);

        var result = await notifications.MarkAsReadAsync(access.User!.Id, id, ct);
        if (result is null)
            return NotFound(new { message = "Thông báo không tồn tại hoặc bạn không có quyền thao tác." });

        return Ok(result);
    }

    [HttpPost("mark-all-read")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken ct = default)
    {
        var access = auth.Evaluate(Request);
        if (access.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(access.Status);

        var result = await notifications.MarkAllAsReadAsync(access.User!.Id, ct);
        return Ok(result);
    }

    [HttpPost("seed")]
    public async Task<IActionResult> SeedSamples(CancellationToken ct = default)
    {
        var access = auth.Evaluate(Request);
        if (access.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(access.Status);

        var count = await notifications.SeedSampleNotificationsAsync(access.User!.Id, ct);
        return Ok(new { success = true, seededCount = count });
    }

    [HttpPost]
    public async Task<IActionResult> CreateNotification(
        [FromBody] CreateNotificationRequest request,
        CancellationToken ct = default)
    {
        var access = auth.Evaluate(Request);
        if (access.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(access.Status);

        var req = (access.User!.Role == "INTERN" || !request.UserId.HasValue)
            ? request with { UserId = access.User!.Id }
            : request;

        var item = await notifications.CreateNotificationAsync(req, access.User!.Id, ct);
        return Created($"/api/notifications/{item.Id}", item);
    }
}
