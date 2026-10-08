using InternManagement.DTOs;
using Microsoft.Data.SqlClient;
using System.Data;

namespace InternManagement.Services;

public sealed class InternAssignmentService : IInternAssignmentService
{
    private readonly string connectionString;

    public InternAssignmentService(IConfiguration configuration)
    {
        connectionString =
            configuration.GetConnectionString("InternManagement")
            ?? throw new InvalidOperationException(
                "Chưa cấu hình connection string InternManagement.");
    }

    public async Task<IReadOnlyList<MentorAssignmentResponse>> GetMentorsAsync(
        CancellationToken cancellationToken)
    {
        var mentors = new List<MentorAssignmentResponse>();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT Id, FullName, Email
            FROM dbo.Users
            WHERE Role = 'MENTOR'
            ORDER BY FullName;
            """;

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            mentors.Add(new MentorAssignmentResponse(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2)
            ));
        }

        return mentors;
    }

    public async Task<IReadOnlyList<InternAssignmentResponse>> GetInternsAsync(
        CancellationToken cancellationToken)
    {
        var interns = new List<InternAssignmentResponse>();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT Id,
                   FullName,
                   Email,
                   School,
                   Major,
                   Mentor
            FROM dbo.Interns
            ORDER BY FullName;
            """;

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            interns.Add(new InternAssignmentResponse(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5)
            ));
        }

        return interns;
    }

    public async Task<InternAssignmentResult?> AssignAsync(
        CreateInternAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var transaction =
            (SqlTransaction)await connection.BeginTransactionAsync(
                cancellationToken);

        // Kiểm tra mentor
        await using var mentorCommand = connection.CreateCommand();
        mentorCommand.Transaction = transaction;

        mentorCommand.CommandText = """
            SELECT Id, FullName, Email
            FROM dbo.Users
            WHERE Id = @mentorId
              AND Role = 'MENTOR';
            """;

        mentorCommand.Parameters.Add(
            "@mentorId",
            SqlDbType.Int).Value = request.MentorId;

        await using var mentorReader =
            await mentorCommand.ExecuteReaderAsync(cancellationToken);

        if (!await mentorReader.ReadAsync(cancellationToken))
        {
            await mentorReader.DisposeAsync();
            await transaction.RollbackAsync(cancellationToken);
            throw new ArgumentException(
                "Mentor không tồn tại hoặc tài khoản không có vai trò MENTOR.");
        }

        var mentorId = mentorReader.GetInt32(0);
        var mentorName = mentorReader.GetString(1);
        var mentorEmail = mentorReader.GetString(2);

        await mentorReader.DisposeAsync();

        // Kiểm tra thực tập sinh
        await using var internCommand = connection.CreateCommand();
        internCommand.Transaction = transaction;

        internCommand.CommandText = """
            SELECT Id, FullName
            FROM dbo.Interns
            WHERE Id = @internId;
            """;

        internCommand.Parameters.Add(
            "@internId",
            SqlDbType.Int).Value = request.InternId;

        await using var internReader =
            await internCommand.ExecuteReaderAsync(cancellationToken);

        if (!await internReader.ReadAsync(cancellationToken))
        {
            await internReader.DisposeAsync();
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var internId = internReader.GetInt32(0);
        var internName = internReader.GetString(1);

        await internReader.DisposeAsync();

        // Lưu phân công
        await using var updateCommand = connection.CreateCommand();
        updateCommand.Transaction = transaction;

        updateCommand.CommandText = """
            UPDATE dbo.Interns
            SET Mentor = @mentorName,
                MentorEmail = @mentorEmail,
                MentorPhone = NULL
            WHERE Id = @internId;
            """;

        updateCommand.Parameters.Add(
            "@mentorName",
            SqlDbType.NVarChar,
            200).Value = mentorName;

        updateCommand.Parameters.Add(
            "@mentorEmail",
            SqlDbType.NVarChar,
            254).Value = mentorEmail;

        updateCommand.Parameters.Add(
            "@internId",
            SqlDbType.Int).Value = internId;

        await updateCommand.ExecuteNonQueryAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new InternAssignmentResult(
            internId,
            internName,
            mentorId,
            mentorName,
            mentorEmail
        );
    }
}