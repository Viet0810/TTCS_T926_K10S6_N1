
namespace InternManagement.DTOs;

public class FinalEvaluationResponse
{
    public int InternId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Mentor { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;

    public decimal? Attendance { get; set; }
    public decimal? Attitude { get; set; }
    public decimal? ProfessionalSkills { get; set; }
    public decimal? WorkResult { get; set; }
    public decimal? FinalScore { get; set; }

    public string Classification { get; set; } = string.Empty;
    public string Status { get; set; } = "Chưa đánh giá";
    public string Comment { get; set; } = string.Empty;
}
