namespace InternManagement.DTOs;

public sealed record InternAttendanceResponse(
    int Id,
    int InternId,
    DateOnly WorkDate,
    DateTimeOffset CheckInAt,
    DateTimeOffset? CheckOutAt);
