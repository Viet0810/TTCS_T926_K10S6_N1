using InternManagement.DTOs;
using Microsoft.Data.SqlClient;

namespace InternManagement.Services;

public class ProgramScheduleService
{
    private readonly string _connectionString;

    public ProgramScheduleService(IConfiguration configuration)
    {
        _connectionString =
            configuration.GetConnectionString("InternManagement")
            ?? throw new InvalidOperationException(
                "Không tìm thấy ConnectionString InternManagement."
            );
    }

    public async Task<List<ProgramScheduleResponse>> GetAllAsync()
    {
        var result = new List<ProgramScheduleResponse>();

        await using var connection =
            new SqlConnection(_connectionString);

        await connection.OpenAsync();

        const string sql = """
            SELECT
                Id,
                StartDate,
                EndDate,
                UpdatedAt
            FROM InternshipPrograms
            ORDER BY Id DESC
            """;

        await using var command =
            new SqlCommand(sql, connection);

        await using var reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var startDate =
                reader.GetDateTime(
                    reader.GetOrdinal("StartDate")
                );

            var endDate =
                reader.GetDateTime(
                    reader.GetOrdinal("EndDate")
                );

            result.Add(
                new ProgramScheduleResponse
                {
                    Id = reader.GetInt32(
                        reader.GetOrdinal("Id")
                    ),

                    StartDate = startDate,

                    EndDate = endDate,

                    DurationDays =
                        (endDate.Date - startDate.Date).Days + 1,

                    Status =
                        GetStatus(startDate, endDate),

                    UpdatedAt =
                        reader.GetDateTime(
                            reader.GetOrdinal("UpdatedAt")
                        )
                }
            );
        }

        return result;
    }

    public async Task<ProgramScheduleResponse> CreateAsync(
        ProgramScheduleRequest request
    )
    {
        if (request.StartDate == default)
        {
            throw new ArgumentException(
                "Ngày bắt đầu không hợp lệ."
            );
        }

        if (request.EndDate == default)
        {
            throw new ArgumentException(
                "Ngày kết thúc không hợp lệ."
            );
        }

        if (request.EndDate.Date < request.StartDate.Date)
        {
            throw new ArgumentException(
                "Ngày kết thúc không được trước ngày bắt đầu."
            );
        }

        await using var connection =
            new SqlConnection(_connectionString);

        await connection.OpenAsync();

        const string sql = """
            INSERT INTO InternshipPrograms
            (
                StartDate,
                EndDate,
                CreatedAt,
                UpdatedAt
            )
            OUTPUT INSERTED.Id
            VALUES
            (
                @StartDate,
                @EndDate,
                GETUTCDATE(),
                GETUTCDATE()
            )
            """;

        await using var command =
            new SqlCommand(sql, connection);

        command.Parameters.AddWithValue(
            "@StartDate",
            request.StartDate.Date
        );

        command.Parameters.AddWithValue(
            "@EndDate",
            request.EndDate.Date
        );

        var id =
            Convert.ToInt32(
                await command.ExecuteScalarAsync()
            );

        return new ProgramScheduleResponse
        {
            Id = id,
            StartDate = request.StartDate.Date,
            EndDate = request.EndDate.Date,

            DurationDays =
                (
                    request.EndDate.Date -
                    request.StartDate.Date
                ).Days + 1,

            Status =
                GetStatus(
                    request.StartDate,
                    request.EndDate
                ),

            UpdatedAt = DateTime.UtcNow
        };
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await using var connection =
            new SqlConnection(_connectionString);

        await connection.OpenAsync();

        const string sql = """
            DELETE FROM InternshipPrograms
            WHERE Id = @Id
            """;

        await using var command =
            new SqlCommand(sql, connection);

        command.Parameters.AddWithValue(
            "@Id",
            id
        );

        var affectedRows =
            await command.ExecuteNonQueryAsync();

        return affectedRows > 0;
    }

    private static string GetStatus(
        DateTime startDate,
        DateTime endDate
    )
    {
        var today = DateTime.Today;

        if (today < startDate.Date)
        {
            return "UPCOMING";
        }

        if (today > endDate.Date)
        {
            return "ENDED";
        }

        return "ACTIVE";
    }
}