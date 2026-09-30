using System.Data;
using System.Text;
using InternManagement.DTOs;
using Microsoft.Data.SqlClient;

namespace InternManagement.HrSearchFilterInterns;

/// <summary>
/// Chức năng: Tìm kiếm và lọc thực tập sinh (K10S6N1-48)
/// Xử lý câu lệnh SQL động với tham số an toàn chống SQL Injection
/// </summary>
public sealed class InternFilterService : IInternFilterService
{
    private readonly string connectionString;

    public InternFilterService(IConfiguration configuration)
    {
        connectionString = configuration.GetConnectionString("InternManagement")
            ?? throw new InvalidOperationException("Chưa cấu hình connection string InternManagement.");
    }

    public async Task<IReadOnlyList<InternResponse>> FilterInternsAsync(InternFilterRequest? filter, CancellationToken cancellationToken = default)
    {
        var interns = new List<InternResponse>();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        var sql = new StringBuilder("SELECT Id, FullName, Email, Phone, School, Major, CreatedAt FROM dbo.Interns WHERE 1 = 1");

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
        {
            interns.Add(new InternResponse(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetDateTimeOffset(6)));
        }

        return interns;
    }
}
