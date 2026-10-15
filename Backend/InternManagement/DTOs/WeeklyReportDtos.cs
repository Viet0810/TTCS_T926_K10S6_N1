using System.ComponentModel.DataAnnotations;

namespace InternManagement.DTOs;

public sealed record CreateWeeklyReportRequest(
    [Required] DateOnly? WeekStartDate,
    [Required, StringLength(4000)] string Content,
    [StringLength(4000)] string? WorkDone=null, [StringLength(4000)] string? Results=null,
    [StringLength(4000)] string? Difficulties=null, [StringLength(4000)] string? NextPlan=null);

public sealed record CreateWeeklyReportFeedbackRequest([Required, StringLength(2000)] string Content);

public sealed record WeeklyReportFeedbackResponse(int Id, int MentorUserId, string MentorName,
    string Content, DateTimeOffset CreatedAt);

public sealed record WeeklyReportResponse(int Id, int InternId, string InternName,
    int? InternshipProgramId, string? ProgramName, DateOnly WeekStartDate, DateOnly WeekEndDate,
    string Content, DateTimeOffset CreatedAt, List<WeeklyReportFeedbackResponse> Feedback,
    string? WorkDone=null,string? Results=null,string? Difficulties=null,string? NextPlan=null);
