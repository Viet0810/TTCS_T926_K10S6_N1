using System.ComponentModel.DataAnnotations;
namespace InternManagement.DTOs;
public sealed record WorkScheduleRequest([Range(1,int.MaxValue)] int InternId,DateOnly WorkDate,TimeOnly StartTime,TimeOnly EndTime,[StringLength(1000)] string? Note);
public sealed record WorkScheduleResponse(int Id,int InternId,string InternName,DateOnly WorkDate,TimeOnly StartTime,TimeOnly EndTime,string? Note);
public sealed record ScheduleInternOption(int Id,string FullName);
