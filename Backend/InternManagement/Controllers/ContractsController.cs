using InternManagement.DTOs;
using InternManagement.Infrastructure;
using InternManagement.Services;
using Microsoft.AspNetCore.Mvc;
namespace InternManagement.Controllers;

[ApiController]
public sealed class ContractsController(ContractService contracts, InternDocumentService documents, RequestAuthorizationService auth) : ControllerBase
{
    private AuthorizationDecision Access(bool hr) => auth.Evaluate(Request, hr?PermissionNames.ManageContracts:PermissionNames.ViewOwnContract);
    private static IActionResult? Denied(AuthorizationDecision decision,bool hr) => decision.Status!=AuthorizationStatus.Authorized
        ? AuthorizationResponses.Denied(decision.Status):decision.User!.Role!=(hr?"HR":"INTERN")?AuthorizationResponses.Denied(AuthorizationStatus.Forbidden):null;
    [HttpGet("api/contracts")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var access=Access(true); if(Denied(access,true) is {} denied)return denied;
        return Ok(await contracts.ListAsync(null,ct));
    }
    [HttpPut("api/contracts/{internId:int}")]
    [RequestSizeLimit(6*1024*1024)]
    [RequestFormLimits(MultipartBodyLengthLimit=6*1024*1024)]
    public async Task<IActionResult> Upload(int internId,IFormFile? file,CancellationToken ct)
    {
        var access=Access(true); if(Denied(access,true) is {} denied)return denied;
        var error=await contracts.UploadAsync(internId,access.User!.Id,file,ct);
        return error is null?Ok(new{message="Đã lưu hợp đồng."}):BadRequest(new{message=error});
    }
    [HttpGet("api/contracts/{internId:int}/file")]
    public async Task<IActionResult> Download(int internId,CancellationToken ct)
    {
        var access=Access(true); if(Denied(access,true) is {} denied)return denied;
        return await DownloadFile(internId,ct);
    }
    [HttpGet("api/interns/me/contract")]
    public async Task<IActionResult> Own(CancellationToken ct)
    {
        var access=Access(false); if(Denied(access,false) is {} denied)return denied;
        var owner=await documents.GetOwnerIdAsync(access.User!.Id,ct);
        if(owner is null)return NotFound(new{message="Chưa có hồ sơ thực tập."});
        return Ok((await contracts.ListAsync(owner.Value,ct)).FirstOrDefault());
    }
    [HttpGet("api/interns/me/contract/file")]
    public async Task<IActionResult> OwnFile(CancellationToken ct)
    {
        var access=Access(false); if(Denied(access,false) is {} denied)return denied;
        var owner=await documents.GetOwnerIdAsync(access.User!.Id,ct);
        return owner is null?NotFound(new{message="Chưa có hồ sơ thực tập."}):await DownloadFile(owner.Value,ct);
    }
    [HttpPut("api/interns/me/contract/confirm")]
    public async Task<IActionResult> Confirm(ConfirmContractRequest request,CancellationToken ct)
    {
        var access=Access(false); if(Denied(access,false) is {} denied)return denied;
        byte[] version; try{version=Convert.FromBase64String(request.Version);}catch(FormatException){return BadRequest(new{message="Phiên bản hợp đồng không hợp lệ."});}
        if(version.Length!=8)return BadRequest(new{message="Phiên bản hợp đồng không hợp lệ."});
        var owner=await documents.GetOwnerIdAsync(access.User!.Id,ct);
        if(owner is null)return NotFound(new{message="Chưa có hồ sơ thực tập."});
        return await contracts.ConfirmAsync(owner.Value,version,ct)?Ok(new{message="Đã xác nhận hợp đồng."}):Conflict(new{message="Hợp đồng đã thay đổi hoặc đã xác nhận. Vui lòng tải lại."});
    }
    private async Task<IActionResult> DownloadFile(int owner,CancellationToken ct)
    {
        var file=await contracts.DownloadAsync(owner,ct);
        return file is null?NotFound(new{message="Chưa có hợp đồng."}):File(file.Content,"application/pdf",file.FileName);
    }
}
