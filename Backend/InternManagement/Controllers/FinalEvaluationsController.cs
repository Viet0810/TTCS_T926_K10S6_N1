
using InternManagement.DTOs;
using InternManagement.Services;
using InternManagement.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace InternManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class FinalEvaluationsController : ControllerBase
{
    private readonly FinalEvaluationService evaluationService;
    private readonly RequestAuthorizationService authorization;

    public FinalEvaluationsController(
        FinalEvaluationService evaluationService,
        RequestAuthorizationService authorization)
    {
        this.evaluationService = evaluationService;
        this.authorization = authorization;
    }

    private static int? MentorScope(AuthorizationDecision decision) =>
        string.Equals(decision.User?.Role, "MENTOR", StringComparison.OrdinalIgnoreCase)
            ? decision.User!.Id : null;

    private static bool IsIntern(string? role) =>
        string.Equals(
            role,
            "INTERN",
            StringComparison.OrdinalIgnoreCase
        );

    private static bool CanEdit(string? role) =>
        string.Equals(role, "ADMIN", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            role,
            "HR",
            StringComparison.OrdinalIgnoreCase
        )
        ||
        string.Equals(
            role,
            "MENTOR",
            StringComparison.OrdinalIgnoreCase
        );

    // GET: api/FinalEvaluations
    [HttpGet]
    public async Task<IActionResult> GetAll(
        CancellationToken cancellationToken)
    {
        var decision = authorization.Evaluate(
            Request,
            PermissionNames.ViewFinalEvaluations
        );

        if (decision.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(decision.Status);

        // INTERN chỉ xem hồ sơ của mình.
        if (IsIntern(decision.User?.Role))
        {
            var internId =
                await evaluationService.GetInternIdByUserIdAsync(
                    decision.User!.Id,
                    cancellationToken
                );

            if (!internId.HasValue)
            {
                return Ok(
                    Array.Empty<FinalEvaluationResponse>()
                );
            }

            var ownResults =
                await evaluationService.GetOwnAsync(
                    internId.Value,
                    cancellationToken
                );

            return Ok(ownResults);
        }

        var results = await evaluationService.GetAllAsync(
            cancellationToken,
            MentorScope(decision)
        );

        return Ok(results);
    }

    // GET: api/FinalEvaluations/statistics
    [HttpGet("statistics")]
    public async Task<IActionResult> GetStatistics(
        CancellationToken cancellationToken)
    {
        var decision = authorization.Evaluate(
            Request,
            PermissionNames.ViewFinalEvaluations
        );

        if (decision.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(decision.Status);

        if (IsIntern(decision.User?.Role))
        {
            var internId =
                await evaluationService.GetInternIdByUserIdAsync(
                    decision.User!.Id,
                    cancellationToken
                );

            if (!internId.HasValue)
            {
                return Ok(new FinalEvaluationStatisticsResponse
                {
                    TotalInterns = 0,
                    EvaluatedCount = 0,
                    PendingCount = 0,
                    AverageScore = null
                });
            }

            var ownResults =
                await evaluationService.GetOwnAsync(
                    internId.Value,
                    cancellationToken
                );

            var evaluated = ownResults
                .Where(x => x.Status == "Đã đánh giá")
                .ToList();

            var scores = evaluated
                .Where(x => x.FinalScore.HasValue)
                .Select(x => x.FinalScore!.Value)
                .ToList();

            return Ok(new FinalEvaluationStatisticsResponse
            {
                TotalInterns = ownResults.Count,
                EvaluatedCount = evaluated.Count,
                PendingCount = ownResults.Count - evaluated.Count,
                AverageScore = scores.Count > 0
                    ? Math.Round(scores.Average(), 2)
                    : null
            });
        }

        var statistics =
            await evaluationService.GetStatisticsAsync(
                cancellationToken,
                MentorScope(decision)
            );

        return Ok(statistics);
    }

    // GET: api/FinalEvaluations/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var decision = authorization.Evaluate(
            Request,
            PermissionNames.ViewFinalEvaluations
        );

        if (decision.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(decision.Status);

        if (id <= 0)
            return BadRequest("InternId phải lớn hơn 0.");

        // INTERN không được truy cập hồ sơ người khác.
        if (IsIntern(decision.User?.Role))
        {
            var internId =
                await evaluationService.GetInternIdByUserIdAsync(
                    decision.User!.Id,
                    cancellationToken
                );

            if (!internId.HasValue || internId.Value != id)
                return StatusCode(StatusCodes.Status403Forbidden);
        }

        var result = await evaluationService.GetByIdAsync(
            id,
            cancellationToken,
            MentorScope(decision)
        );

        if (result is null && MentorScope(decision).HasValue)
            return StatusCode(StatusCodes.Status403Forbidden);

        if (result is null)
            return NotFound("Không tìm thấy thực tập sinh.");

        return Ok(result);
    }

    // POST: api/FinalEvaluations/{id}
    [HttpPost("{id:int}")]
    public async Task<IActionResult> Create(
        int id,
        [FromBody] FinalEvaluationRequest request,
        CancellationToken cancellationToken)
    {
        var decision = authorization.Evaluate(
            Request,
            PermissionNames.UpdateFinalEvaluations
        );

        if (decision.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(decision.Status);

        if (!CanEdit(decision.User?.Role))
            return StatusCode(StatusCodes.Status403Forbidden);

        if (id <= 0)
            return BadRequest("InternId phải lớn hơn 0.");

        if (request is null)
            return BadRequest("Dữ liệu đánh giá không hợp lệ.");

        var intern = await evaluationService.GetByIdAsync(
            id,
            cancellationToken,
            MentorScope(decision)
        );

        if (intern is null && MentorScope(decision).HasValue)
            return StatusCode(StatusCodes.Status403Forbidden);

        if (intern is null)
            return NotFound("Không tìm thấy thực tập sinh.");

        var created = await evaluationService.CreateAsync(
            id,
            request,
            cancellationToken,
            MentorScope(decision),decision.User!.Id
        );

        if (!created)
        {
            return Conflict(
                "Thực tập sinh đã có đánh giá. Hãy sử dụng chức năng cập nhật."
            );
        }

        var result = await evaluationService.GetByIdAsync(
            id,
            cancellationToken,
            MentorScope(decision)
        );

        return CreatedAtAction(
            nameof(GetById),
            new { id },
            result
        );
    }

    // PUT: api/FinalEvaluations/{id}
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] FinalEvaluationRequest request,
        CancellationToken cancellationToken)
    {
        var decision = authorization.Evaluate(
            Request,
            PermissionNames.UpdateFinalEvaluations
        );

        if (decision.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(decision.Status);

        if (!CanEdit(decision.User?.Role))
            return StatusCode(StatusCodes.Status403Forbidden);

        if (id <= 0)
            return BadRequest("InternId phải lớn hơn 0.");

        if (request is null)
            return BadRequest("Dữ liệu đánh giá không hợp lệ.");

        var intern = await evaluationService.GetByIdAsync(
            id,
            cancellationToken,
            MentorScope(decision)
        );

        if (intern is null && MentorScope(decision).HasValue)
            return StatusCode(StatusCodes.Status403Forbidden);

        if (intern is null)
            return NotFound("Không tìm thấy thực tập sinh.");

        var updated = await evaluationService.UpdateAsync(
            id,
            request,
            cancellationToken,
            MentorScope(decision),decision.User!.Id
        );

        if (!updated)
        {
            return NotFound(
                "Thực tập sinh chưa có đánh giá để cập nhật."
            );
        }

        var result = await evaluationService.GetByIdAsync(
            id,
            cancellationToken,
            MentorScope(decision)
        );

        return Ok(result);
    }
}
