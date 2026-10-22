using System.ComponentModel.DataAnnotations;

namespace InternManagement.DTOs;

[method: System.Text.Json.Serialization.JsonConstructor]
public sealed record CreateMeetingRequest(
    [Required, StringLength(200)] string Title,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    [StringLength(500)] string? LocationOrLink,
    [StringLength(2000)] string? Description,
    [Required, MinLength(1)] IReadOnlyList<int> ParticipantUserIds,
    Guid ClientRequestId)
{
    public CreateMeetingRequest(string title,DateTimeOffset startTime,string? locationOrLink,string? description,IReadOnlyList<int> participantUserIds)
        : this(title,startTime,startTime.AddHours(1),locationOrLink,description,participantUserIds,Guid.NewGuid()) { }
}

public sealed record MeetingResponse(
    int Id,
    string Title,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string? LocationOrLink,
    string? Description,
    int CreatedByUserId,
    IReadOnlyList<int> ParticipantUserIds,
    IReadOnlyList<MeetingParticipantOption> Participants,
    int EmailsSent,
    int EmailsFailed,
    DateTimeOffset CreatedAt = default);

public sealed record MeetingParticipantOption(int Id, string FullName, string Email, string Role);
