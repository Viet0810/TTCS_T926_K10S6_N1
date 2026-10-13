using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InternManagement.DTOs;

namespace InternManagement.Services;

public sealed class InternWorkScheduleService : IInternWorkScheduleService
{
    private static readonly List<InternWorkScheduleDto> Schedules = new();
    private static readonly object Locker = new();
    private static int _nextId = 1;

    // Lấy tất cả lịch làm việc
    public Task<List<InternWorkScheduleDto>> GetAllAsync()
    {
        lock (Locker)
        {
            var result = Schedules.Select(Copy).ToList();
            return Task.FromResult(result);
        }
    }

    // Lấy lịch làm việc theo mã thực tập sinh
    public Task<List<InternWorkScheduleDto>> GetByInternIdAsync(int internId)
    {
        lock (Locker)
        {
            var result = Schedules
                .Where(x => x.InternId == internId)
                .Select(Copy)
                .ToList();

            return Task.FromResult(result);
        }
    }

    // Tạo lịch làm việc
    public Task<InternWorkScheduleDto> CreateAsync(CreateInternWorkScheduleDto dto)
    {
        Validate(dto.InternId, dto.StartTime, dto.EndTime);

        lock (Locker)
        {
            var schedule = new InternWorkScheduleDto
            {
                Id = _nextId++,
                InternId = dto.InternId,
                WorkDate = dto.WorkDate.Date,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                Note = dto.Note
            };

            Schedules.Add(schedule);

            return Task.FromResult(Copy(schedule));
        }
    }

    // Cập nhật lịch làm việc
    public Task<bool> UpdateAsync(int id, UpdateInternWorkScheduleDto dto)
    {
        // Chỉ kiểm tra giờ giấc hợp lệ, bỏ qua việc kiểm tra InternId
        if (dto.StartTime < TimeSpan.Zero || dto.StartTime >= TimeSpan.FromDays(1) ||
            dto.EndTime < TimeSpan.Zero || dto.EndTime >= TimeSpan.FromDays(1) ||
            dto.EndTime <= dto.StartTime)
        {
            throw new ArgumentException("Giờ làm việc không hợp lệ.");
        }

        lock (Locker)
        {
            var schedule = Schedules.FirstOrDefault(x => x.Id == id);

            if (schedule == null)
            {
                return Task.FromResult(false);
            }

            // Bỏ qua dòng gán schedule.InternId = dto.InternId;
            schedule.WorkDate = dto.WorkDate.Date;
            schedule.StartTime = dto.StartTime;
            schedule.EndTime = dto.EndTime;
            schedule.Note = dto.Note;

            return Task.FromResult(true);
        }
    }

    // Kiểm tra dữ liệu đầu vào (áp dụng cho CreateAsync)
    private static void Validate(int internId, TimeSpan startTime, TimeSpan endTime)
    {
        if (internId <= 0)
        {
            throw new ArgumentException("InternId phải lớn hơn 0.");
        }

        if (startTime < TimeSpan.Zero ||
            startTime >= TimeSpan.FromDays(1) ||
            endTime < TimeSpan.Zero ||
            endTime >= TimeSpan.FromDays(1))
        {
            throw new ArgumentException("Giờ làm việc không hợp lệ.");
        }

        if (endTime <= startTime)
        {
            throw new ArgumentException("Giờ kết thúc phải sau giờ bắt đầu.");
        }
    }

    // Sao chép dữ liệu để trả về
    private static InternWorkScheduleDto Copy(InternWorkScheduleDto source)
    {
        return new InternWorkScheduleDto
        {
            Id = source.Id,
            InternId = source.InternId,
            WorkDate = source.WorkDate,
            StartTime = source.StartTime,
            EndTime = source.EndTime,
            Note = source.Note
        };
    }
}