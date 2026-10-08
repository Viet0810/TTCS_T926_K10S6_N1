using InternManagement.DTOs;

namespace InternManagement.Services
{
    public interface IAssignmentService
    {
        Task<List<MentorDto>> GetMentorsAsync();
        Task<List<InternDto>> GetInternsAsync();
        Task<bool> AssignMentorAsync(AssignMentorRequest request);
    }
}