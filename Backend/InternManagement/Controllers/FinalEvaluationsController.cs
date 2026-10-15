
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

    // GET: api/FinalEvaluations
    // Lấy danh sách đánh giá cuối kỳ.
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

        var results = await evaluationService.GetAllAsync(
            cancellationToken
        );

        return Ok(results);
    }

    // GET: api/FinalEvaluations/statistics
    // Thống kê kết quả đánh giá cuối kỳ.
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

        var statistics = await evaluationService.GetStatisticsAsync(
            cancellationToken
        );

        return Ok(statistics);
    }

    // GET: api/FinalEvaluations/{id}
    // Xem đánh giá của thực tập sinh theo InternId.
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

        var result = await evaluationService.GetByIdAsync(
            id,
            cancellationToken
        );

        if (result is null)
            return NotFound("Không tìm thấy thực tập sinh.");

        return Ok(result);
    }

    // POST: api/FinalEvaluations/{id}
    // HR thêm đánh giá cho thực tập sinh chưa được đánh giá.
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

        // Chỉ HR được phép thêm đánh giá.
        if (!string.Equals(
                decision.User?.Role,
                "HR",
                StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        if (id <= 0)
            return BadRequest("InternId phải lớn hơn 0.");

        if (request is null)
            return BadRequest("Dữ liệu đánh giá không hợp lệ.");

        var intern = await evaluationService.GetByIdAsync(
            id,
            cancellationToken
        );

        if (intern is null)
            return NotFound("Không tìm thấy thực tập sinh.");

        var created = await evaluationService.CreateAsync(
            id,
            request,
            cancellationToken
        );

        if (!created)
        {
            return Conflict(
                "Thực tập sinh đã có đánh giá. Hãy sử dụng chức năng cập nhật."
            );
        }

        var result = await evaluationService.GetByIdAsync(
            id,
            cancellationToken
        );

        return CreatedAtAction(
            nameof(GetById),
            new { id },
            result
        );
    }

    // PUT: api/FinalEvaluations/{id}
    // HR cập nhật đánh giá cuối kỳ của thực tập sinh.
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

        // Chỉ HR được phép sửa đánh giá.
        if (!string.Equals(
                decision.User?.Role,
                "HR",
                StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        if (id <= 0)
            return BadRequest("InternId phải lớn hơn 0.");

        if (request is null)
            return BadRequest("Dữ liệu đánh giá không hợp lệ.");

        var intern = await evaluationService.GetByIdAsync(
            id,
            cancellationToken
        );

        if (intern is null)
            return NotFound("Không tìm thấy thực tập sinh.");

        var updated = await evaluationService.UpdateAsync(
            id,
            request,
            cancellationToken
        );

        if (!updated)
        {
            return NotFound(
                "Thực tập sinh chưa có đánh giá để cập nhật."
            );
        }

        var result = await evaluationService.GetByIdAsync(
            id,
            cancellationToken
        );

        return Ok(result);
    }
}
