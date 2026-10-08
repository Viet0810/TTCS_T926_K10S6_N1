using InternManagement.DTOs;
using Npgsql;
using NpgsqlTypes;
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
        await using var connection = new NpgsqlConnection(connectionString);
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
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {InternRecordMapper.Columns} FROM dbo.Interns WHERE Id = @id";
        command.Parameters.Add("@id", NpgsqlDbType.Integer).Value = id;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? InternRecordMapper.Map(reader) : null;
    }

    public async Task<InternResponse?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {InternRecordMapper.Columns} FROM dbo.Interns WHERE LOWER(Email) = LOWER(@email)";
        command.Parameters.Add("@email", NpgsqlDbType.Varchar, 254).Value = email;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? InternRecordMapper.Map(reader) : null;
    }

    public async Task<InternResponse> CreateAsync(CreateInternRequest request, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO dbo.Interns (FullName, Email, Phone, School, Major, StudentCode, ClassName, Faculty, DateOfBirth, Address, Organization, OrganizationAddress, Department, Position, Mentor, MentorEmail, MentorPhone, AcademicSupervisor, StartDate, EndDate, Status, InternshipTopic, Notes)
            VALUES (@fullName, @email, @phone, @school, @major, @studentCode, @className, @faculty, @dateOfBirth, @address, @organization, @organizationAddress, @department, @position, @mentor, @mentorEmail, @mentorPhone, @academicSupervisor, @startDate, @endDate, @status, @internshipTopic, @notes)
            RETURNING Id, FullName, Email, Phone, School, Major, CreatedAt, StudentCode, ClassName, Faculty, DateOfBirth, Address, Organization, OrganizationAddress, Department, Position, Mentor, MentorEmail, MentorPhone, AcademicSupervisor, StartDate, EndDate, Status, InternshipTopic, Notes;
            """;
        InternRecordMapper.AddParameters(command, request);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return InternRecordMapper.Map(reader);
    }

    public async Task<InternResponse?> UpdateAsync(int id, CreateInternRequest request, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE dbo.Interns SET FullName = @fullName, Email = @email, Phone = @phone, School = @school, Major = @major, StudentCode = @studentCode, ClassName = @className, Faculty = @faculty, DateOfBirth = @dateOfBirth, Address = @address, Organization = @organization, OrganizationAddress = @organizationAddress, Department = @department, Position = @position, Mentor = @mentor, MentorEmail = @mentorEmail, MentorPhone = @mentorPhone, AcademicSupervisor = @academicSupervisor, StartDate = @startDate, EndDate = @endDate, Status = @status, InternshipTopic = @internshipTopic, Notes = @notes
            WHERE Id = @id
            RETURNING Id, FullName, Email, Phone, School, Major, CreatedAt, StudentCode, ClassName, Faculty, DateOfBirth, Address, Organization, OrganizationAddress, Department, Position, Mentor, MentorEmail, MentorPhone, AcademicSupervisor, StartDate, EndDate, Status, InternshipTopic, Notes;
            """;
        command.Parameters.Add("@id", NpgsqlDbType.Integer).Value = id;
        InternRecordMapper.AddParameters(command, request);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? InternRecordMapper.Map(reader) : null;
    }

    public async Task<InternStatusUpdateResult?> UpdateStatusAsync(int id, string? status, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            UPDATE dbo.Interns SET Status=@status
            WHERE Id=@id AND Status IS DISTINCT FROM @status
            RETURNING {InternRecordMapper.Columns}
            """;
        command.Parameters.Add("@id", NpgsqlDbType.Integer).Value = id;
        command.Parameters.Add("@status", NpgsqlDbType.Varchar, 50).Value = (object?)status ?? DBNull.Value;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
                return new InternStatusUpdateResult(InternRecordMapper.Map(reader), true);
        }

        var existing = await GetByIdAsync(id, cancellationToken);
        return existing is null ? null : new InternStatusUpdateResult(existing, false);
    }
}
