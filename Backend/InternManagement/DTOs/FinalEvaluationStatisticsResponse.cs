
namespace InternManagement.DTOs;

public class FinalEvaluationStatisticsResponse
{
    public int TotalInterns { get; set; }

    public int EvaluatedCount { get; set; }

    public int PendingCount { get; set; }

    public decimal? AverageScore { get; set; }
}
