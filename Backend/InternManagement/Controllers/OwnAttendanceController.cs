using InternManagement.Infrastructure;
using InternManagement.Services;
using Microsoft.AspNetCore.Mvc;
namespace InternManagement.Controllers;
[ApiController]
[Route("api/interns/me/attendance")]
public sealed class OwnAttendanceController(OwnAttendanceService service,InternDocumentService documents,RequestAuthorizationService auth):ControllerBase
{
    private async Task<(int? Owner,IActionResult? Denied)> Owner(CancellationToken ct)
    {
        var access=auth.Evaluate(Request,PermissionNames.OwnAttendance);
        if(access.Status!=AuthorizationStatus.Authorized)return(null,AuthorizationResponses.Denied(access.Status));
        if(access.User!.Role!="INTERN")return(null,AuthorizationResponses.Denied(AuthorizationStatus.Forbidden));
        var owner=await documents.GetOwnerIdAsync(access.User.Id,ct);
        return owner is null?(null,NotFound(new{message="Chưa có hồ sơ thực tập."})):(owner,null);
    }
    [HttpGet]
    public async Task<IActionResult> Today(CancellationToken ct)
    {
        var owner=await Owner(ct);return owner.Denied??Ok(await service.TodayAsync(owner.Owner!.Value,ct));
    }
    [HttpPost("check-in")]
    public Task<IActionResult> CheckIn(CancellationToken ct)=>Record(false,ct);
    [HttpPost("check-out")]
    public Task<IActionResult> CheckOut(CancellationToken ct)=>Record(true,ct);
    private async Task<IActionResult> Record(bool checkOut,CancellationToken ct)
    {
        var owner=await Owner(ct);if(owner.Denied is {} denied)return denied;
        var error=await service.RecordAsync(owner.Owner!.Value,checkOut,ct);
        return error is null?Ok(new{message=checkOut?"Đã check-out.":"Đã check-in."}):Conflict(new{message=error});
    }
}
