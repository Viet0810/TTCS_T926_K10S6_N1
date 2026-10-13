
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using InternManagement.DTOs;
using InternManagement.Services;

namespace InternManagement.Controllers;

[ApiController]
[Route("api/intern-work-schedules")]
[Authorize]
public class InternWorkSchedulesController : ControllerBase
{
    private readonly IInternWorkScheduleService _service;

    public InternWorkSchedulesController(
        IInternWorkScheduleService service)
    {
        _service = service;
    }

    // POST: api/intern-work-schedules
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateInternWorkScheduleDto dto)
    {
        try
        {
            int id = await _service.CreateAsync(dto);

            return Created(
                $"/api/intern-work-schedules/{id}",
                new
                {
                    message = "Tạo lịch làm việc thành công.",
                    id
                });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (SqlException ex) when (
            ex.Number == 2601 || ex.Number == 2627)
        {
            return Conflict(new
            {
                message = "Intern đã có lịch làm việc trong ngày này."
            });
        }
    }

    // PUT: api/intern-work-schedules/{id}
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateInternWorkScheduleDto dto)
    {
        if (id <= 0)
            return BadRequest(new { message = "ID không hợp lệ." });

        try
        {
            bool updated = await _service.UpdateAsync(id, dto);

            if (!updated)
                return NotFound(new
                {
                    message = "Không tìm thấy lịch làm việc."
                });

            return Ok(new
            {
                message = "Cập nhật lịch làm việc thành công."
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (SqlException ex) when (
            ex.Number == 2601 || ex.Number == 2627)
        {
            return Conflict(new
            {
                message = "Intern đã có lịch làm việc trong ngày này."
            });
        }
    }

    // GET: api/intern-work-schedules/{internId}
    [HttpGet("{internId:int}")]
    public async Task<IActionResult> GetByInternId(int internId)
    {
        if (internId <= 0)
            return BadRequest(new { message = "InternId không hợp lệ." });

        var schedules = await _service.GetByInternIdAsync(internId);
        return Ok(schedules);
    }
}
