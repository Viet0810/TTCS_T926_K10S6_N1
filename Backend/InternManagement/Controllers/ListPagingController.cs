using InternManagement.DTOs;
using InternManagement.Infrastructure;
using InternManagement.Services;
using Microsoft.AspNetCore.Mvc;
namespace InternManagement.Controllers;
[ApiController]
public sealed class ListPagingController(ListPagingService lists,RequestAuthorizationService authorization):ControllerBase
{
    [HttpGet("api/interns/page")]
    public async Task<IActionResult> Interns(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var decision=authorization.Evaluate(Request,PermissionNames.ViewInterns);
        if(decision.Status!=AuthorizationStatus.Authorized)return AuthorizationResponses.Denied(decision.Status);
        if (!TryCreatePageRequest(page, pageSize, search, out var request, out var error)) return error!;
        return Ok(await lists.InternsAsync(request!,null,ct));
    }
    [HttpGet("api/users/page")]
    public async Task<IActionResult> Users(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var decision=authorization.Evaluate(Request,PermissionNames.ManageUsers);
        if(decision.Status!=AuthorizationStatus.Authorized)return AuthorizationResponses.Denied(decision.Status);
        if (!TryCreatePageRequest(page, pageSize, search, out var request, out var error)) return error!;
        return Ok(await lists.UsersAsync(request!,ct,decision.User!.Role));
    }
    [HttpGet("api/interns/search/page")]
    public async Task<IActionResult> Search(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] InternFilterRequest? filter = null,
        CancellationToken ct = default)
    {
        var decision=authorization.Evaluate(Request,PermissionNames.SearchInterns);
        if(decision.Status!=AuthorizationStatus.Authorized)return AuthorizationResponses.Denied(decision.Status);
        if (!TryCreatePageRequest(page, pageSize, null, out var request, out var error)) return error!;
        return Ok(await lists.InternsAsync(request!,filter,ct));
    }
    [HttpGet("api/interns/search/options")]
    public async Task<IActionResult> Options(CancellationToken ct)
    {
        var decision=authorization.Evaluate(Request,PermissionNames.SearchInterns);
        if(decision.Status!=AuthorizationStatus.Authorized)return AuthorizationResponses.Denied(decision.Status);
        return Ok(await lists.InternOptionsAsync(ct));
    }

    private bool TryCreatePageRequest(
        int page,
        int pageSize,
        string? search,
        out PageRequest? request,
        out IActionResult? error)
    {
        if (page < 1 || page > 1_000_000 || pageSize < 1 || pageSize > 100 || search?.Length > 254)
        {
            request = null;
            error = BadRequest(new ApiErrorResponse(false, "Tham số phân trang không hợp lệ.", null));
            return false;
        }

        request = new PageRequest { Page = page, PageSize = pageSize, Search = search };
        error = null;
        return true;
    }
}
