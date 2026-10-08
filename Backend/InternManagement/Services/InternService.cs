using InternManagement.DTOs;
using Microsoft.Data.SqlClient;
using System.Data;

namespace InternManagement.Services;

public sealed class InternService : IInternService
{
    private readonly string connectionString;

    public InternService(IConfiguration configuration)
    {
        connectionString = configuration.GetConnectionString("InternManagement")
            ?? throw new InvalidOperationException("Chưa cấu hình connection string InternManagement.");
    }

    public async Task<IReadOnlyList<InternResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        var interns = new List<InternResponse>();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {InternRecordMapper.Columns} FROM dbo.Interns ORDER BY Id DESC";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            interns.Add(InternRecordMapper.Map(reader));

        return interns;
    }

    public async Task<InternResponse?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {InternRecordMapper.Columns} FROM dbo.Interns WHERE Id = @id";
        command.Parameters.Add("@id", SqlDbType.Int).Value = id;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? InternRecordMapper.Map(reader) : null;
    }

    public async Task<InternResponse?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {InternRecordMapper.Columns} FROM dbo.Interns WHERE Email = @email";
        command.Parameters.Add("@email", SqlDbType.NVarChar, 254).Value = email;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? InternRecordMapper.Map(reader) : null;
    }

    public async Task<InternResponse> CreateAsync(CreateInternRequest request, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO dbo.Interns (FullName, Email, Phone, School, Major, StudentCode, ClassName, Faculty, DateOfBirth, Address, Organization, OrganizationAddress, Department, Position, Mentor, MentorEmail, MentorPhone, AcademicSupervisor, StartDate, EndDate, Status, InternshipTopic, Notes)
            OUTPUT INSERTED.Id, INSERTED.FullName, INSERTED.Email, INSERTED.Phone, INSERTED.School, INSERTED.Major, INSERTED.CreatedAt, INSERTED.StudentCode, INSERTED.ClassName, INSERTED.Faculty, INSERTED.DateOfBirth, INSERTED.Address, INSERTED.Organization, INSERTED.OrganizationAddress, INSERTED.Department, INSERTED.Position, INSERTED.Mentor, INSERTED.MentorEmail, INSERTED.MentorPhone, INSERTED.AcademicSupervisor, INSERTED.StartDate, INSERTED.EndDate, INSERTED.Status, INSERTED.InternshipTopic, INSERTED.Notes
            VALUES (@fullName, @email, @phone, @school, @major, @studentCode, @className, @faculty, @dateOfBirth, @address, @organization, @organizationAddress, @department, @position, @mentor, @mentorEmail, @mentorPhone, @academicSupervisor, @startDate, @endDate, @status, @internshipTopic, @notes);
            """;
        InternRecordMapper.AddParameters(command, request);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return InternRecordMapper.Map(reader);
    }

    public async Task<InternResponse?> UpdateAsync(int id, CreateInternRequest request, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE dbo.Interns SET FullName = @fullName, Email = @email, Phone = @phone, School = @school, Major = @major, StudentCode = @studentCode, ClassName = @className, Faculty = @faculty, DateOfBirth = @dateOfBirth, Address = @address, Organization = @organization, OrganizationAddress = @organizationAddress, Department = @department, Position = @position, Mentor = @mentor, MentorEmail = @mentorEmail, MentorPhone = @mentorPhone, AcademicSupervisor = @academicSupervisor, StartDate = @startDate, EndDate = @endDate, Status = @status, InternshipTopic = @internshipTopic, Notes = @notes
            OUTPUT INSERTED.Id, INSERTED.FullName, INSERTED.Email, INSERTED.Phone, INSERTED.School, INSERTED.Major, INSERTED.CreatedAt, INSERTED.StudentCode, INSERTED.ClassName, INSERTED.Faculty, INSERTED.DateOfBirth, INSERTED.Address, INSERTED.Organization, INSERTED.OrganizationAddress, INSERTED.Department, INSERTED.Position, INSERTED.Mentor, INSERTED.MentorEmail, INSERTED.MentorPhone, INSERTED.AcademicSupervisor, INSERTED.StartDate, INSERTED.EndDate, INSERTED.Status, INSERTED.InternshipTopic, INSERTED.Notes
            WHERE Id = @id;
            """;
        command.Parameters.Add("@id", SqlDbType.Int).Value = id;
        InternRecordMapper.AddParameters(command, request);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? InternRecordMapper.Map(reader) : null;
    }
}
