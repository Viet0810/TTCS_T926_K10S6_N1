namespace InternManagement.DTOs;

public class ProgramScheduleResponse
{
    public string? Name { get; set; }
    public string? Department { get; set; }
    public int Id { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public int DurationDays { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; }
}
