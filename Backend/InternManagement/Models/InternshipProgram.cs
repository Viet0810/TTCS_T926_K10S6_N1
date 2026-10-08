namespace InternManagement.Models;

public class InternshipProgram
{
    public string? Name { get; set; }
    public string? Department { get; set; }
    public int Id { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
