using InternManagement.DTOs;

namespace InternManagement.Services;

public interface IInternService
{
    Task<InternResponse?> UpdateOwnAsync(int userId, UpdateOwnInternRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<InternResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<InternResponse?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<InternResponse?> GetByEmailAsync(string email, CancellationToken cancellationToken);
    Task<InternResponse> CreateAsync(CreateInternRequest request, CancellationToken cancellationToken);
    Task<InternResponse?> UpdateAsync(int id, CreateInternRequest request, CancellationToken cancellationToken);
}
