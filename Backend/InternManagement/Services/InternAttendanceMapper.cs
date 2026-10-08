using InternManagement.DTOs;
using Microsoft.Data.SqlClient;

namespace InternManagement.Services;

internal static class InternAttendanceMapper
{
    public static InternAttendanceResponse Map(SqlDataReader reader) => new(
        reader.GetInt32(0),
        reader.GetInt32(1),
        DateOnly.FromDateTime(reader.GetDateTime(2)),
        reader.GetDateTimeOffset(3),
        reader.IsDBNull(4) ? null : reader.GetDateTimeOffset(4));
}
