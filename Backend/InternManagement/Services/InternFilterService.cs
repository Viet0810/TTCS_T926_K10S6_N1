using System.Data;
using System.Text;
using InternManagement.DTOs;
using Microsoft.Data.SqlClient;

namespace InternManagement.Services;

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

        var sql = new StringBuilder($"SELECT {InternRecordMapper.Columns} FROM dbo.Interns WHERE 1 = 1");

        if (!string.IsNullOrWhiteSpace(filter?.Search))
        {
            sql.Append(" AND (FullName LIKE @search OR Email LIKE @search OR Phone LIKE @search OR School LIKE @search OR Major LIKE @search OR StudentCode LIKE @search OR ClassName LIKE @search OR Organization LIKE @search OR Position LIKE @search OR Mentor LIKE @search)");
            command.Parameters.Add("@search", SqlDbType.NVarChar, 256).Value = $"%{filter.Search.Trim()}%";
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
            interns.Add(InternRecordMapper.Map(reader));
        }

        return interns;
    }
}
