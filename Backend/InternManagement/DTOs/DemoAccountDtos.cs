namespace InternManagement.DTOs;

public sealed record DemoAccountResponse(
    int Id,
    string FullName,
    string Email,
    string Role,
    string? EmployeeCode,
    string? StudentCode,
    string? MentorName,
    bool IsDemoAccount);

public sealed record DemoSeedResult(
    int Admins,
    int HumanResources,
    int Mentors,
    int Interns,
    int Programs,
    int Assignments,
    int Contracts,
    int AttendanceRecords,
    int Documents,
    int Evaluations,
    string AccountsFile);
