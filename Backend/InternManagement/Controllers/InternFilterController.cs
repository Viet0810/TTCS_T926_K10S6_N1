using InternManagement.Infrastructure;
using InternManagement.DTOs;
using InternManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace InternManagement.Controllers;

/// <summary>
/// Chức năng: Tìm kiếm và lọc thực tập sinh (K10S6N1-48)
/// Controller API riêng cho chức năng tìm kiếm & lọc từ giao diện HR
/// </summary>
[ApiController]
[Route("api/interns/search")]
public sealed class InternFilterController : ControllerBase
{
    private readonly IInternFilterService filterService;
    private readonly RequestAuthorizationService authorization;

    public InternFilterController(IInternFilterService filterService, RequestAuthorizationService authorization)
    {
        this.filterService = filterService;
        this.authorization = authorization;
    }

    /// <summary>Tìm kiếm và lọc danh sách thực tập sinh theo từ khóa, trường đào tạo, chuyên ngành.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<InternResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SearchInterns([FromQuery] InternFilterRequest? filter, CancellationToken cancellationToken)
    {
        var decision = authorization.Evaluate(Request, PermissionNames.SearchInterns);
        if (decision.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(decision.Status);

        var result = await filterService.FilterInternsAsync(filter, cancellationToken);
        return Ok(result);
    }

}
