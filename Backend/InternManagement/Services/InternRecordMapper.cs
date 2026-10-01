using System.Data;
using InternManagement.DTOs;
using Microsoft.Data.SqlClient;
namespace InternManagement.Services;

internal static class InternRecordMapper
{
    public const string Columns = "Id, FullName, Email, Phone, School, Major, CreatedAt, StudentCode, ClassName, Faculty, DateOfBirth, Address, Organization, OrganizationAddress, Department, Position, Mentor, MentorEmail, MentorPhone, AcademicSupervisor, StartDate, EndDate, Status, InternshipTopic, Notes";
    public static InternResponse Map(SqlDataReader reader) => new(
        reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
        reader.GetString(4), reader.GetString(5), reader.GetDateTimeOffset(6),
        reader.IsDBNull(7) ? null : reader.GetString(7),
        reader.IsDBNull(8) ? null : reader.GetString(8),
        reader.IsDBNull(9) ? null : reader.GetString(9),
        reader.IsDBNull(10) ? null : DateOnly.FromDateTime(reader.GetDateTime(10)),
        reader.IsDBNull(11) ? null : reader.GetString(11),
        reader.IsDBNull(12) ? null : reader.GetString(12),
        reader.IsDBNull(13) ? null : reader.GetString(13),
        reader.IsDBNull(14) ? null : reader.GetString(14),
        reader.IsDBNull(15) ? null : reader.GetString(15),
        reader.IsDBNull(16) ? null : reader.GetString(16),
        reader.IsDBNull(17) ? null : reader.GetString(17),
        reader.IsDBNull(18) ? null : reader.GetString(18),
        reader.IsDBNull(19) ? null : reader.GetString(19),
        reader.IsDBNull(20) ? null : DateOnly.FromDateTime(reader.GetDateTime(20)),
        reader.IsDBNull(21) ? null : DateOnly.FromDateTime(reader.GetDateTime(21)),
        reader.IsDBNull(22) ? null : reader.GetString(22),
        reader.IsDBNull(23) ? null : reader.GetString(23),
        reader.IsDBNull(24) ? null : reader.GetString(24));

    public static void AddParameters(SqlCommand command, CreateInternRequest request)
    {
        AddText(command, "@fullName", 200, request.FullName);
        AddText(command, "@email", 254, request.Email);
        AddText(command, "@phone", 20, request.Phone);
        AddText(command, "@school", 200, request.School);
        AddText(command, "@major", 200, request.Major);
        AddText(command, "@studentCode", 50, request.StudentCode);
        AddText(command, "@className", 100, request.ClassName);
        AddText(command, "@faculty", 200, request.Faculty);
        command.Parameters.Add("@dateOfBirth", SqlDbType.Date).Value = request.DateOfBirth.HasValue ? request.DateOfBirth.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value;
        AddText(command, "@address", 500, request.Address);
        AddText(command, "@organization", 200, request.Organization);
        AddText(command, "@organizationAddress", 500, request.OrganizationAddress);
        AddText(command, "@department", 200, request.Department);
        AddText(command, "@position", 200, request.Position);
        AddText(command, "@mentor", 200, request.Mentor);
        AddText(command, "@mentorEmail", 254, request.MentorEmail);
        AddText(command, "@mentorPhone", 20, request.MentorPhone);
        AddText(command, "@academicSupervisor", 200, request.AcademicSupervisor);
        command.Parameters.Add("@startDate", SqlDbType.Date).Value = request.StartDate.HasValue ? request.StartDate.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value;
        command.Parameters.Add("@endDate", SqlDbType.Date).Value = request.EndDate.HasValue ? request.EndDate.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value;
        AddText(command, "@status", 50, request.Status);
        AddText(command, "@internshipTopic", 500, request.InternshipTopic);
        AddText(command, "@notes", 2000, request.Notes);
    }
    private static void AddText(SqlCommand command, string name, int length, string? value)
    {
        command.Parameters.Add(name, SqlDbType.NVarChar, length).Value =
            string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    }
}
