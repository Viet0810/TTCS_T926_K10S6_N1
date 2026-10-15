using InternManagement.DTOs;
using InternManagement.Infrastructure;
using InternManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace InternManagement.Controllers;

[ApiController]
[Route("api/interns/me/leave-requests")]
public sealed class LeaveRequestsController(
    ILeaveRequestService leaveService,
    RequestAuthorizationService auth
) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateLeaveRequest([FromBody] CreateLeaveRequestDto dto, CancellationToken ct = default)
    {
        var access = auth.Evaluate(Request);
        if (access.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(access.Status);

        if (access.User?.Role is not ("INTERN" or "ADMIN"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Chức năng này chỉ dành cho Thực tập sinh." });
        }

        if (dto is null)
        {
            return BadRequest(new { message = "Dữ liệu đơn xin nghỉ phép không được để trống." });
        }

        try
        {
            var result = await leaveService.CreateLeaveRequestAsync(access.User!.Id, dto, ct);
            return StatusCode(StatusCodes.Status201Created, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Đã xảy ra lỗi trong quá trình xử lý yêu cầu.", detail = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetMyLeaveRequests(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 15,
        CancellationToken ct = default)
    {
        var access = auth.Evaluate(Request);
        if (access.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(access.Status);

        if (access.User?.Role is not ("INTERN" or "ADMIN"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Chức năng này chỉ dành cho Thực tập sinh." });
        }

        try
        {
            var result = await leaveService.GetMyLeaveRequestsAsync(access.User!.Id, status, page, pageSize, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Đã xảy ra lỗi trong quá trình tải danh sách đơn nghỉ phép.", detail = ex.Message });
        }
    }

    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> CancelLeaveRequest([FromRoute] long id, CancellationToken ct = default)
    {
        var access = auth.Evaluate(Request);
        if (access.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(access.Status);

        if (access.User?.Role is not ("INTERN" or "ADMIN"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Chức năng này chỉ dành cho Thực tập sinh." });
        }

        if (id <= 0)
        {
            return BadRequest(new { message = "Mã đơn xin nghỉ phép không hợp lệ." });
        }

        try
        {
            var result = await leaveService.CancelLeaveRequestAsync(access.User!.Id, id, ct);
            if (!result.Success)
            {
                if (result.Status == "NOT_FOUND")
                {
                    return NotFound(new { message = result.Message });
                }
                return BadRequest(new { message = result.Message });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Đã xảy ra lỗi khi hủy đơn xin nghỉ.", detail = ex.Message });
        }
    }
}
