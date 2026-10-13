
using System.ComponentModel.DataAnnotations;

namespace InternManagement.DTOs;

public class CreateInternWorkScheduleDto
{
    [Required]
    public int InternId { get; set; }

    [Required]
    public DateTime WorkDate { get; set; }

    [Required]
    public TimeSpan StartTime { get; set; }

    [Required]
    public TimeSpan EndTime { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }
}

public class UpdateInternWorkScheduleDto
{
    [Required]
    public DateTime WorkDate { get; set; }

    [Required]
    public TimeSpan StartTime { get; set; }

    [Required]
    public TimeSpan EndTime { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }
}

public class InternWorkScheduleDto
{
    public int Id { get; set; }
    public int InternId { get; set; }
    public DateTime WorkDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string? Note { get; set; }
}
