using InternManagement.DTOs;
using InternManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace InternManagement.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AssignmentController : ControllerBase
    {
        private readonly IAssignmentService _assignmentService;

        public AssignmentController(IAssignmentService assignmentService)
        {
            _assignmentService = assignmentService;
        }

        // GET: api/Assignment/mentors
        [HttpGet("mentors")]
        public async Task<IActionResult> GetMentors()
        {
            var result = await _assignmentService.GetMentorsAsync();
            return Ok(result);
        }

        // GET: api/Assignment/interns
        [HttpGet("interns")]
        public async Task<IActionResult> GetInterns()
        {
            var result = await _assignmentService.GetInternsAsync();
            return Ok(result);
        }

        // POST: api/Assignment/assign
        [HttpPost("assign")]
        public async Task<IActionResult> AssignMentor([FromBody] AssignMentorRequest request)
        {
            if (request == null || request.InternIds == null || !request.InternIds.Any())
            {
                return BadRequest(new { message = "Dữ liệu không hợp lệ." });
            }

            var success = await _assignmentService.AssignMentorAsync(request);
            if (!success) return BadRequest(new { message = "Không tìm thấy mentor hoặc thực tập sinh để phân công." });
            return Ok(new { message = "Lưu phân công thành công!" });
        }
    }
}