using InternManagement.DTOs;
using InternManagement.Infrastructure;
using InternManagement.Services;
using Microsoft.AspNetCore.Mvc;
namespace InternManagement.Controllers;
[ApiController,Route("api/work-schedules")]
public sealed class WorkSchedulesController(WorkScheduleService schedules,RequestAuthorizationService authorization):ControllerBase
{
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) {
        var a=authorization.Evaluate(Request,PermissionNames.ReadWorkSchedules);
        if(a.Status!=AuthorizationStatus.Authorized)return AuthorizationResponses.Denied(a.Status);
        return Ok(await schedules.ListAsync(a.User!.Id,a.User.Role=="INTERN",null,ct));
    }
    [HttpGet("{id:int}")] public async Task<IActionResult> Get(int id,CancellationToken ct) {
        var a=authorization.Evaluate(Request,PermissionNames.ReadWorkSchedules);
        if(a.Status!=AuthorizationStatus.Authorized)return AuthorizationResponses.Denied(a.Status);
        var rows=await schedules.ListAsync(a.User!.Id,a.User.Role=="INTERN",id,ct);
        return rows.Count==0?StatusCode(403):Ok(rows[0]);
    }
    [HttpGet("interns")] public async Task<IActionResult> Interns(CancellationToken ct) {
        var a=authorization.Evaluate(Request,PermissionNames.ReadWorkSchedules);
        if(a.Status!=AuthorizationStatus.Authorized)return AuthorizationResponses.Denied(a.Status);
        if(a.User!.Role=="INTERN")return StatusCode(403);
        return Ok(await schedules.InternsAsync(ct));
    }
    [HttpPost] public Task<IActionResult> Create(WorkScheduleRequest request,CancellationToken ct)=>Save(null,request,ct);
    [HttpPut("{id:int}")] public Task<IActionResult> Update(int id,WorkScheduleRequest request,CancellationToken ct)=>Save(id,request,ct);
    private async Task<IActionResult> Save(int? id,WorkScheduleRequest request,CancellationToken ct) {
        var a=authorization.Evaluate(Request,id is null?PermissionNames.CreateWorkSchedule:PermissionNames.UpdateWorkSchedule);
        if(a.Status!=AuthorizationStatus.Authorized)return AuthorizationResponses.Denied(a.Status);
        try {var result=await schedules.SaveAsync(a.User!.Id,id,request,ct);return result is null?StatusCode(403):Ok(new{id=result});}
        catch(ArgumentException e){return BadRequest(new{message=e.Message});}
        catch(InvalidOperationException e){return Conflict(new{message=e.Message});}
    }
}