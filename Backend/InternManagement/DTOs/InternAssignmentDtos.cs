namespace InternManagement.DTOs;

public sealed record MentorAssignmentResponse(
    int Id,
    string FullName,
    string Email
);

public sealed record InternAssignmentResponse(
    int Id,
    string FullName,
    string Email,
    string School,
    string Major,
    string? Mentor
);

public sealed record CreateInternAssignmentRequest(
    int MentorId,
    int InternId
);

public sealed record InternAssignmentResult(
    int InternId,
    string InternName,
    int MentorId,
    string MentorName,
    string MentorEmail
);