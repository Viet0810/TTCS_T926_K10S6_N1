using InternManagement.DTOs;

namespace InternManagement.Services;

public interface IInternService
{
    Task<IReadOnlyList<InternResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<InternResponse?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<InternResponse?> GetByEmailAsync(string email, CancellationToken cancellationToken);
    Task<InternResponse> CreateAsync(CreateInternRequest request, CancellationToken cancellationToken);
    Task<InternResponse?> UpdateAsync(int id, CreateInternRequest request, CancellationToken cancellationToken);
    Task<InternStatusUpdateResult?> UpdateStatusAsync(int id, string? status, CancellationToken cancellationToken);
}

public sealed record InternStatusUpdateResult(InternResponse Intern, bool Changed);
