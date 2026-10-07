using InternManagement.DTOs;
using InternManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace InternManagement.Controllers;

[ApiController]
[Route("api/program-schedule")]
public class ProgramScheduleController : ControllerBase
{
    private readonly ProgramScheduleService _service;

    public ProgramScheduleController(
        ProgramScheduleService service
    )
    {
        _service = service;
    }

    // GET api/program-schedule
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
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
        catch (Exception ex)
        {
            return StatusCode(
                500,
                new
                {
                    status = "error",
                    message =
                        "Không thể lấy danh sách chương trình.",
                    error = ex.Message
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
        catch (Exception ex)
        {
            return StatusCode(
                500,
                new
                {
                    status = "error",
                    message =
                        "Không thể lưu thời gian chương trình.",
                    error = ex.Message
                }
            );
        }
    }

    // DELETE api/program-schedule/1
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
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
        catch (Exception ex)
        {
            return StatusCode(
                500,
                new
                {
                    status = "error",
                    message =
                        "Không thể xóa chương trình.",
                    error = ex.Message
                }
            );
        }
    }
}