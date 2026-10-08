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

    public async Task<InternResponse?> UpdateOwnAsync(int userId, UpdateOwnInternRequest request, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        // Ownership comes exclusively from the authenticated account, never from the request.
        command.CommandText = """
            UPDATE i SET FullName = @name, Phone = @phone, School = @school, Major = @major, Address = @address
            FROM dbo.Interns i INNER JOIN dbo.Users u ON u.Email = i.Email
            WHERE u.Id = @userId AND u.Role = 'INTERN';
            """;
        command.Parameters.Add("@userId", SqlDbType.Int).Value = userId;
        command.Parameters.Add("@name", SqlDbType.NVarChar, 200).Value = request.FullName;
        command.Parameters.Add("@phone", SqlDbType.NVarChar, 20).Value = request.Phone;
        command.Parameters.Add("@school", SqlDbType.NVarChar, 200).Value = request.School;
        command.Parameters.Add("@major", SqlDbType.NVarChar, 200).Value = request.Major;
        command.Parameters.Add("@address", SqlDbType.NVarChar, 500).Value = (object?)request.Address?.Trim() ?? DBNull.Value;
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0) return null;
        command.CommandText = "UPDATE dbo.Users SET FullName = @name WHERE Id = @userId;";
        await command.ExecuteNonQueryAsync(cancellationToken);
        command.CommandText = $"SELECT {InternRecordMapper.Columns} FROM dbo.Interns WHERE Email = (SELECT Email FROM dbo.Users WHERE Id = @userId)";
        InternResponse? result;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            result = await reader.ReadAsync(cancellationToken) ? InternRecordMapper.Map(reader) : null;
        await transaction.CommitAsync(cancellationToken);
        return result;
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
