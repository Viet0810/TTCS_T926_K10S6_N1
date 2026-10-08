using System.Data;
using InternManagement.DTOs;
using Microsoft.Data.SqlClient;

namespace InternManagement.Services
{
    public class AssignmentService : IAssignmentService
    {
        private readonly string _connectionString;

        public AssignmentService(IConfiguration configuration)
        {
            // Lấy chuỗi kết nối "InternManagement" từ appsettings.json
            _connectionString = configuration.GetConnectionString("InternManagement") 
                ?? throw new InvalidOperationException("Không tìm thấy connection string 'InternManagement'");
        }

        // 1. Lấy danh sách Mentor
        public async Task<List<MentorDto>> GetMentorsAsync()
        {
            var mentors = new List<MentorDto>();

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = "SELECT Id, FullName, Email FROM dbo.Users WHERE Role = 'MENTOR'";
            using var command = new SqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                mentors.Add(new MentorDto
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    FullName = reader.GetString(reader.GetOrdinal("FullName")),
                    Email = reader.GetString(reader.GetOrdinal("Email"))
                });
            }

            return mentors;
        }

        // 2. Lấy danh sách Thực tập sinh
        public async Task<List<InternDto>> GetInternsAsync()
        {
            var interns = new List<InternDto>();

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = "SELECT Id, FullName, Email, Mentor, MentorEmail FROM dbo.Interns";
            using var command = new SqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var mentorOrdinal = reader.GetOrdinal("Mentor");
                var mentorEmailOrdinal = reader.GetOrdinal("MentorEmail");

                interns.Add(new InternDto
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    FullName = reader.GetString(reader.GetOrdinal("FullName")),
                    Email = reader.GetString(reader.GetOrdinal("Email")),
                    Mentor = reader.IsDBNull(mentorOrdinal) ? null : reader.GetString(mentorOrdinal),
                    MentorEmail = reader.IsDBNull(mentorEmailOrdinal) ? null : reader.GetString(mentorEmailOrdinal)
                });
            }

            return interns;
        }

        // 3. Lưu phân công Mentor cho Thực tập sinh
        public async Task<bool> AssignMentorAsync(AssignMentorRequest request)
        {
            if (request.InternIds == null || !request.InternIds.Any())
                return false;

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Lấy thông tin Mentor từ bảng Users
            var mentorQuery = "SELECT FullName, Email FROM dbo.Users WHERE Id = @MentorId AND Role = 'MENTOR'";
            using var mentorCommand = new SqlCommand(mentorQuery, connection);
            mentorCommand.Parameters.AddWithValue("@MentorId", request.MentorId);

            string? mentorName = null;
            string? mentorEmail = null;

            using (var reader = await mentorCommand.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    mentorName = reader.GetString(reader.GetOrdinal("FullName"));
                    mentorEmail = reader.GetString(reader.GetOrdinal("Email"));
                }
                else
                {
                    return false; // Không tìm thấy mentor
                }
            }

            // Cập nhật thông tin Mentor cho các thực tập sinh
            var parameters = new List<string>();
            for (int i = 0; i < request.InternIds.Count; i++)
            {
                parameters.Add($"@Id{i}");
            }
            var internIdsStr = string.Join(",", parameters);

            var query = $"UPDATE dbo.Interns SET Mentor = @Mentor, MentorEmail = @MentorEmail WHERE Id IN ({internIdsStr})";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@Mentor", (object?)mentorName ?? DBNull.Value);
            command.Parameters.AddWithValue("@MentorEmail", (object?)mentorEmail ?? DBNull.Value);
            
            for (int i = 0; i < request.InternIds.Count; i++)
            {
                command.Parameters.AddWithValue($"@Id{i}", request.InternIds[i]);
            }

            var rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }
    }
}