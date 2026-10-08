using InternManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace InternManagement.Controllers;

[ApiController]
[Route("api/push")]
public sealed class PushController(WebPushService push, RequestAuthorizationService authorization) : ControllerBase
{
    private int? CurrentUserId()
    {
        var decision = authorization.Evaluate(Request);
        if (decision.Status == AuthorizationStatus.Authorized) return decision.User!.Id;
        Response.StatusCode = decision.Status == AuthorizationStatus.Unauthenticated ? 401 : 403;
        return null;
    }

    [HttpGet("public-key")]
    public IActionResult PublicKey() => push.PublicKey is { } key
        ? Ok(new { publicKey = key })
        : StatusCode(503, new { message = "Máy chủ chưa cấu hình Web Push." });

    [HttpPost("subscriptions")]
    public async Task<IActionResult> Subscribe(SubscriptionRequest request, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        if (userId is null) return StatusCode(Response.StatusCode, new { message = "Vui lòng đăng nhập." });
        if (!Uri.TryCreate(request.Endpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme != Uri.UriSchemeHttps
            || string.IsNullOrWhiteSpace(request.Keys?.P256dh) || request.Keys.P256dh.Length > 500
            || string.IsNullOrWhiteSpace(request.Keys.Auth) || request.Keys.Auth.Length > 500)
            return BadRequest(new { message = "Thông tin đăng ký thông báo không hợp lệ." });
        await push.SaveSubscriptionAsync(userId.Value, endpoint.AbsoluteUri, request.Keys.P256dh,
            request.Keys.Auth, cancellationToken);
        return Ok(new { subscribed = true });
    }

    [HttpDelete("subscriptions")]
    public async Task<IActionResult> Unsubscribe(EndpointRequest request, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        if (userId is null) return StatusCode(Response.StatusCode, new { message = "Vui lòng đăng nhập." });
        if (!Uri.TryCreate(request.Endpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme != Uri.UriSchemeHttps)
            return BadRequest(new { message = "Endpoint không hợp lệ." });
        await push.RemoveSubscriptionAsync(userId.Value, endpoint.AbsoluteUri, cancellationToken);
        return Ok(new { unsubscribed = true });
    }

    public sealed record SubscriptionRequest(string Endpoint, SubscriptionKeys? Keys);
    public sealed record EndpointRequest(string Endpoint);
    public sealed record SubscriptionKeys(string P256dh, string Auth);
}
