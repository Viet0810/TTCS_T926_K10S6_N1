using System.Data;
using InternManagement.DTOs;
using Microsoft.Data.SqlClient;

namespace InternManagement.Services;

public sealed class InternAttendanceService(IConfiguration configuration)
{
    private static readonly TimeSpan BusinessTimeZoneOffset = TimeSpan.FromHours(7);
    private readonly string connectionString = configuration.GetConnectionString("InternManagement")
        ?? throw new InvalidOperationException("Chưa cấu hình connection string InternManagement.");

    public async Task<InternAttendanceResponse?> GetTodayAsync(int internId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToOffset(BusinessTimeZoneOffset);
        var workDate = new DateOnly(now.Year, now.Month, now.Day);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, InternId, WorkDate, CheckInAt, CheckOutAt
            FROM dbo.InternAttendances
            WHERE InternId = @internId AND WorkDate = @workDate;
            """;
        command.Parameters.Add("@internId", SqlDbType.Int).Value = internId;
        command.Parameters.Add("@workDate", SqlDbType.Date).Value = workDate.ToDateTime(TimeOnly.MinValue);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? InternAttendanceMapper.Map(reader) : null;
    }

    public async Task<InternAttendanceResponse> CheckInAsync(int internId, CancellationToken cancellationToken)
    {
        var checkInAt = DateTimeOffset.UtcNow.ToOffset(BusinessTimeZoneOffset);
        var workDate = new DateOnly(checkInAt.Year, checkInAt.Month, checkInAt.Day);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO dbo.InternAttendances (InternId, WorkDate, CheckInAt)
            OUTPUT INSERTED.Id, INSERTED.InternId, INSERTED.WorkDate, INSERTED.CheckInAt, INSERTED.CheckOutAt
            VALUES (@internId, @workDate, @checkInAt);
            """;
        command.Parameters.Add("@internId", SqlDbType.Int).Value = internId;
        command.Parameters.Add("@workDate", SqlDbType.Date).Value = workDate.ToDateTime(TimeOnly.MinValue);
        command.Parameters.Add("@checkInAt", SqlDbType.DateTimeOffset).Value = checkInAt;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("Không nhận được bản ghi attendance vừa tạo.");

        return InternAttendanceMapper.Map(reader);
    }

    public async Task<InternAttendanceResponse?> CheckOutAsync(int internId, CancellationToken cancellationToken)
    {
        var checkOutAt = DateTimeOffset.UtcNow.ToOffset(BusinessTimeZoneOffset);
        var workDate = new DateOnly(checkOutAt.Year, checkOutAt.Month, checkOutAt.Day);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE dbo.InternAttendances
            SET CheckOutAt = @checkOutAt
            OUTPUT INSERTED.Id, INSERTED.InternId, INSERTED.WorkDate, INSERTED.CheckInAt, INSERTED.CheckOutAt
            WHERE InternId = @internId AND WorkDate = @workDate AND CheckOutAt IS NULL;
            """;
        command.Parameters.Add("@checkOutAt", SqlDbType.DateTimeOffset).Value = checkOutAt;
        command.Parameters.Add("@internId", SqlDbType.Int).Value = internId;
        command.Parameters.Add("@workDate", SqlDbType.Date).Value = workDate.ToDateTime(TimeOnly.MinValue);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? InternAttendanceMapper.Map(reader) : null;
    }
}
