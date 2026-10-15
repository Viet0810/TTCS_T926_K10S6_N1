
using System.ComponentModel.DataAnnotations;

namespace InternManagement.DTOs;

public sealed class FinalEvaluationRequest
{
    [Required]
    [StringLength(200)]
    public required string Period { get; set; }

    [Range(0, 10)]
    public decimal Attendance { get; set; }

    [Range(0, 10)]
    public decimal Attitude { get; set; }

    [Range(0, 10)]
    public decimal ProfessionalSkills { get; set; }

    [Range(0, 10)]
    public decimal WorkResult { get; set; }

    [Range(0, 10)]
    public decimal FinalScore { get; set; }

    [Required]
    [StringLength(50)]
    public required string Classification { get; set; }

    [StringLength(2000)]
    public string? Comment { get; set; }
}
