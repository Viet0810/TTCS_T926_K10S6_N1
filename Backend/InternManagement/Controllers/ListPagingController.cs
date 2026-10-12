using InternManagement.DTOs;
using InternManagement.Infrastructure;
using InternManagement.Services;
using Microsoft.AspNetCore.Mvc;
namespace InternManagement.Controllers;
[ApiController]
public sealed class ListPagingController(ListPagingService lists,RequestAuthorizationService authorization):ControllerBase
{
    [HttpGet("api/interns/page")]
    public async Task<IActionResult> Interns([FromQuery]PageRequest page,CancellationToken ct)
    {
        var decision=authorization.Evaluate(Request,PermissionNames.ViewInterns);
        if(decision.Status!=AuthorizationStatus.Authorized)return AuthorizationResponses.Denied(decision.Status);
        return Ok(await lists.InternsAsync(page,null,ct));
    }
    [HttpGet("api/users/page")]
    public async Task<IActionResult> Users([FromQuery]PageRequest page,CancellationToken ct)
    {
        var decision=authorization.Evaluate(Request,PermissionNames.ManageUsers);
        if(decision.Status!=AuthorizationStatus.Authorized)return AuthorizationResponses.Denied(decision.Status);
        return Ok(await lists.UsersAsync(page,ct));
    }
    [HttpGet("api/interns/search/page")]
    public async Task<IActionResult> Search([FromQuery]PageRequest page,[FromQuery]InternFilterRequest filter,CancellationToken ct)
    {
        var decision=authorization.Evaluate(Request,PermissionNames.SearchInterns);
        if(decision.Status!=AuthorizationStatus.Authorized)return AuthorizationResponses.Denied(decision.Status);
        return Ok(await lists.InternsAsync(page,filter,ct));
    }
    [HttpGet("api/interns/search/options")]
    public async Task<IActionResult> Options(CancellationToken ct)
    {
        var decision=authorization.Evaluate(Request,PermissionNames.SearchInterns);
        if(decision.Status!=AuthorizationStatus.Authorized)return AuthorizationResponses.Denied(decision.Status);
        return Ok(await lists.InternOptionsAsync(ct));
    }
}
