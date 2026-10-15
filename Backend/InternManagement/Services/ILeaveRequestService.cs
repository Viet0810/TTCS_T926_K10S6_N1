using InternManagement.DTOs;

namespace InternManagement.Services;

public interface ILeaveRequestService
{
    Task<LeaveRequestResponseDto> CreateLeaveRequestAsync(int userId, CreateLeaveRequestDto dto, CancellationToken ct = default);
    Task<LeaveRequestListResponse> GetMyLeaveRequestsAsync(int userId, string? status = null, int page = 1, int pageSize = 15, CancellationToken ct = default);
    Task<CancelLeaveRequestResponse> CancelLeaveRequestAsync(int userId, long requestId, CancellationToken ct = default);
}
