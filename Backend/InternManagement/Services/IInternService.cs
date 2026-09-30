using InternManagement.DTOs;

namespace InternManagement.Services;

public interface IInternService
{
    Task<IReadOnlyList<InternResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<InternResponse?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<InternResponse?> GetByEmailAsync(string email, CancellationToken cancellationToken);
    Task<InternResponse> CreateAsync(CreateInternRequest request, CancellationToken cancellationToken);
}