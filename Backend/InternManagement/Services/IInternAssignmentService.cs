using InternManagement.DTOs;

namespace InternManagement.Services;

public interface IInternAssignmentService
{
    Task<IReadOnlyList<MentorAssignmentResponse>> GetMentorsAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<InternAssignmentResponse>> GetInternsAsync(
        CancellationToken cancellationToken);

    Task<InternAssignmentResult?> AssignAsync(
        CreateInternAssignmentRequest request,
        CancellationToken cancellationToken);
}