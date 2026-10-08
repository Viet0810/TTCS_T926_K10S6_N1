using InternManagement.DTOs;
using InternManagement.Infrastructure;
using InternManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace InternManagement.Controllers;

[ApiController]
[Route("api/program-schedule")]
public class ProgramScheduleController : ControllerBase
{
    private readonly ProgramScheduleService _service;
    private readonly RequestAuthorizationService authorization;

    public ProgramScheduleController(
        ProgramScheduleService service,
        RequestAuthorizationService authorization
    )
    {
        _service = service;
        this.authorization = authorization;
    }

    private IActionResult? CheckAccess()
    {
        var decision = authorization.Evaluate(Request, PermissionNames.ManagePrograms);
        if (decision.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(decision.Status);
        return decision.User!.Role == "HR" ? null : AuthorizationResponses.Denied(AuthorizationStatus.Forbidden);
    }

    // GET api/program-schedule
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        if (CheckAccess() is { } denied) return denied;
        try
        {
            var data =
                await _service.GetAllAsync();

            return Ok(
                new
                {
                    status = "success",
                    message =
                        "Lấy danh sách chương trình thành công.",
                    data
                }
            );
        }
        catch (Exception)
        {
            return StatusCode(
                500,
                new
                {
                    status = "error",
                    message =
                        "Không thể lấy danh sách chương trình."
                }
            );
        }
    }

    // POST api/program-schedule
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] ProgramScheduleRequest request
    )
    {
        if (CheckAccess() is { } denied) return denied;
        try
        {
            var data =
                await _service.CreateAsync(request);

            return Ok(
                new
                {
                    status = "success",
                    message =
                        "Lưu thời gian chương trình thành công.",
                    data
                }
            );
        }
        catch (ArgumentException ex)
        {
            return BadRequest(
                new
                {
                    status = "error",
                    message = ex.Message,
                    data = (object?)null
                }
            );
        }
        catch (Exception)
        {
            return StatusCode(
                500,
                new
                {
                    status = "error",
                    message =
                        "Không thể lưu thời gian chương trình."
                }
            );
        }
    }

    // DELETE api/program-schedule/1
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (CheckAccess() is { } denied) return denied;
        try
        {
            var deleted =
                await _service.DeleteAsync(id);

            if (!deleted)
            {
                return NotFound(
                    new
                    {
                        status = "error",
                        message =
                            "Không tìm thấy chương trình cần xóa."
                    }
                );
            }

            return Ok(
                new
                {
                    status = "success",
                    message =
                        "Xóa chương trình thành công."
                }
            );
        }
        catch (Exception)
        {
            return StatusCode(
                500,
                new
                {
                    status = "error",
                    message =
                        "Không thể xóa chương trình."
                }
            );
        }
    }
}
