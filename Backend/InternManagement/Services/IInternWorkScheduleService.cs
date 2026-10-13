using System.Collections.Generic;
using System.Threading.Tasks;
using InternManagement.DTOs;

namespace InternManagement.Services;

public interface IInternWorkScheduleService
{
    Task<List<InternWorkScheduleDto>> GetAllAsync();

    Task<List<InternWorkScheduleDto>> GetByInternIdAsync(int internId);

    Task<InternWorkScheduleDto> CreateAsync(CreateInternWorkScheduleDto dto);

    Task<bool> UpdateAsync(int id, UpdateInternWorkScheduleDto dto);
}