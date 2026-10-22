using System.ComponentModel.DataAnnotations;

namespace InternManagement.DTOs;

public sealed record EvaluationSummaryListItem(
    int InternId, string? StudentCode, string FullName, string Email,
    int? MentorId, string? MentorName, int? ProgramId, string? ProgramName,
    int EvaluationCount, int RequiredEvaluationCount, decimal? AverageScore,
    string TemporaryClassification, string Status, string StatusLabel);

public sealed record MentorEvaluationItem(
    int Id, string MentorName, string Period, decimal? Attendance, decimal? Attitude,
    decimal? ProfessionalSkills, decimal? WorkResult, decimal? FinalScore,
    string? Classification, string? Comment, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

public sealed record EvaluationSummaryHistoryItem(
    long Id, string Action, string ActorName, string? AggregateComment,
    string? FinalClassification, DateTimeOffset OccurredAt);

public sealed record EvaluationSummaryDetail(
    EvaluationSummaryListItem Intern,
    IReadOnlyList<MentorEvaluationItem> Evaluations,
    string? AggregateComment,
    string? FinalClassification,
    int? FinalizedByUserId,
    string? FinalizedByName,
    DateTimeOffset? FinalizedAt,
    int? PublishedByUserId,
    string? PublishedByName,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<EvaluationSummaryHistoryItem> History);

public sealed record SaveEvaluationSummaryRequest : IValidatableObject
{
    [Required, StringLength(2000)] public string AggregateComment { get; init; } = "";
    [Required, StringLength(50)] public string FinalClassification { get; init; } = "";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(AggregateComment))
            yield return new ValidationResult("Nhận xét tổng hợp là bắt buộc.", [nameof(AggregateComment)]);
        if (string.IsNullOrWhiteSpace(FinalClassification))
            yield return new ValidationResult("Xếp loại cuối cùng là bắt buộc.", [nameof(FinalClassification)]);
    }
}

public sealed record PublishedEvaluationResult(
    int InternId, string? StudentCode, string FullName, string? ProgramName,
    decimal AverageScore, string FinalClassification, string AggregateComment,
    DateTimeOffset PublishedAt);
