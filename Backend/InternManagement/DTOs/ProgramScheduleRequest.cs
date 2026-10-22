namespace InternManagement.DTOs;

public class ProgramScheduleRequest
{
    [System.ComponentModel.DataAnnotations.StringLength(200)]
    public string? Name { get; set; }
    [System.ComponentModel.DataAnnotations.StringLength(200)]
    public string? Department { get; set; }
    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }
}
