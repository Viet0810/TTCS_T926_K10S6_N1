using InternManagement.DTOs;
using InternManagement.Infrastructure;
using InternManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace InternManagement.Controllers;

[ApiController]
[Route("api/evaluation-summaries")]
public sealed class EvaluationSummariesController(
    EvaluationSummaryService summaries,
    RequestAuthorizationService authorization) : ControllerBase
{
    private AuthorizationDecision Authorize(string permission) => authorization.Evaluate(Request, permission);

    private static bool IsHrOrAdmin(AuthorizationDecision decision) =>
        decision.User?.Role is not null &&
        (decision.User.Role.Equals("HR", StringComparison.OrdinalIgnoreCase) ||
         decision.User.Role.Equals("ADMIN", StringComparison.OrdinalIgnoreCase));

    private IActionResult? Denied(AuthorizationDecision decision)
    {
        if (decision.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(decision.Status);
        return IsHrOrAdmin(decision) ? null : StatusCode(StatusCodes.Status403Forbidden);
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null, [FromQuery] int? programId = null,
        [FromQuery] int? mentorId = null, [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        var decision = Authorize(PermissionNames.ViewEvaluationSummaries);
        var denied = Denied(decision);
        return denied ?? Ok(await summaries.GetPageAsync(page, pageSize, search, programId, mentorId, status, ct));
    }

    [HttpGet("filters")]
    public async Task<IActionResult> Filters(CancellationToken ct)
    {
        var decision = Authorize(PermissionNames.ViewEvaluationSummaries);
        var denied = Denied(decision);
        return denied ?? Ok(await summaries.GetFilterOptionsAsync(ct));
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] string? search, [FromQuery] int? programId,
        [FromQuery] int? mentorId, [FromQuery] string? status, CancellationToken ct)
    {
        var decision = Authorize(PermissionNames.ViewEvaluationSummaries);
        var denied = Denied(decision);
        if (denied is not null) return denied;
        var content = await summaries.ExportCsvAsync(search, programId, mentorId, status, ct);
        return File(content, "text/csv; charset=utf-8", $"tong-hop-danh-gia-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet("my-result")]
    public async Task<IActionResult> MyResult(CancellationToken ct)
    {
        var decision = Authorize(PermissionNames.ViewFinalEvaluations);
        if (decision.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(decision.Status);
        if (!string.Equals(decision.User?.Role, "INTERN", StringComparison.OrdinalIgnoreCase))
            return StatusCode(StatusCodes.Status403Forbidden);
        var result = await summaries.GetPublishedForUserAsync(decision.User!.Id, ct);
        return result is null ? NotFound(new { message = "Kết quả chưa được HR công bố." }) : Ok(result);
    }

    [HttpGet("{internId:int}")]
    public async Task<IActionResult> Detail(int internId, CancellationToken ct)
    {
        var decision = Authorize(PermissionNames.ViewEvaluationSummaries);
        var denied = Denied(decision);
        if (denied is not null) return denied;
        var result = await summaries.GetDetailAsync(internId, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("{internId:int}")]
    public async Task<IActionResult> Save(int internId, SaveEvaluationSummaryRequest request, CancellationToken ct)
    {
        var decision = Authorize(PermissionNames.FinalizeEvaluationSummaries);
        var denied = Denied(decision);
        if (denied is not null) return denied;
        return await summaries.SaveAsync(internId, decision.User!.Id, request, ct)
            ? Ok(await summaries.GetDetailAsync(internId, ct))
            : Conflict(new { message = "Không thể sửa kết quả đã chốt hoặc đã công bố." });
    }

    [HttpPost("{internId:int}/finalize")]
    public async Task<IActionResult> Finalize(int internId, SaveEvaluationSummaryRequest request, CancellationToken ct)
    {
        var decision = Authorize(PermissionNames.FinalizeEvaluationSummaries);
        var denied = Denied(decision);
        if (denied is not null) return denied;
        return await summaries.FinalizeAsync(internId, decision.User!.Id, request, ct)
            ? Ok(await summaries.GetDetailAsync(internId, ct))
            : Conflict(new { message = "Chưa đủ đánh giá hoặc kết quả đã được chốt." });
    }

    [HttpPost("{internId:int}/publish")]
    public async Task<IActionResult> Publish(int internId, CancellationToken ct)
    {
        var decision = Authorize(PermissionNames.PublishEvaluationSummaries);
        var denied = Denied(decision);
        if (denied is not null) return denied;
        return await summaries.PublishAsync(internId, decision.User!.Id, ct)
            ? Ok(await summaries.GetDetailAsync(internId, ct))
            : Conflict(new { message = "Chỉ có thể công bố kết quả đã chốt." });
    }
}
