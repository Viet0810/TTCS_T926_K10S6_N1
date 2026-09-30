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

    public async Task<IReadOnlyList<InternResponse>> GetAllAsync(InternFilterRequest? filter = null, CancellationToken cancellationToken = default)
    {
        var interns = new List<InternResponse>();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        var sql = new System.Text.StringBuilder("SELECT Id, FullName, Email, Phone, School, Major, CreatedAt FROM dbo.Interns WHERE 1 = 1");

        if (!string.IsNullOrWhiteSpace(filter?.Search))
        {
            sql.Append(" AND (FullName LIKE @search OR Email LIKE @search OR Phone LIKE @search)");
            command.Parameters.Add("@search", SqlDbType.NVarChar, 254).Value = $"%{filter.Search.Trim()}%";
        }

        if (!string.IsNullOrWhiteSpace(filter?.School))
        {
            sql.Append(" AND School = @school");
            command.Parameters.Add("@school", SqlDbType.NVarChar, 200).Value = filter.School.Trim();
        }

        if (!string.IsNullOrWhiteSpace(filter?.Major))
        {
            sql.Append(" AND Major = @major");
            command.Parameters.Add("@major", SqlDbType.NVarChar, 200).Value = filter.Major.Trim();
        }

        sql.Append(" ORDER BY Id DESC");
        command.CommandText = sql.ToString();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            interns.Add(Map(reader));

        return interns;
    }

    public async Task<InternResponse?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, FullName, Email, Phone, School, Major, CreatedAt FROM dbo.Interns WHERE Id = @id";
        command.Parameters.Add("@id", SqlDbType.Int).Value = id;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    public async Task<InternResponse?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, FullName, Email, Phone, School, Major, CreatedAt FROM dbo.Interns WHERE Email = @email";
        command.Parameters.Add("@email", SqlDbType.NVarChar, 254).Value = email;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    public async Task<InternResponse> CreateAsync(CreateInternRequest request, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO dbo.Interns (FullName, Email, Phone, School, Major)
            OUTPUT INSERTED.Id, INSERTED.FullName, INSERTED.Email, INSERTED.Phone,
                   INSERTED.School, INSERTED.Major, INSERTED.CreatedAt
            VALUES (@fullName, @email, @phone, @school, @major);
            """;
        command.Parameters.Add("@fullName", SqlDbType.NVarChar, 200).Value = request.FullName.Trim();
        command.Parameters.Add("@email", SqlDbType.NVarChar, 254).Value = request.Email.Trim();
        command.Parameters.Add("@phone", SqlDbType.NVarChar, 20).Value = request.Phone.Trim();
        command.Parameters.Add("@school", SqlDbType.NVarChar, 200).Value = request.School.Trim();
        command.Parameters.Add("@major", SqlDbType.NVarChar, 200).Value = request.Major.Trim();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return Map(reader);
    }

    private static InternResponse Map(SqlDataReader reader) => new(
        reader.GetInt32(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetString(4),
        reader.GetString(5),
        reader.GetDateTimeOffset(6));
}