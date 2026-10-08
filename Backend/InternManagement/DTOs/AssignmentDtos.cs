namespace InternManagement.DTOs
{
    // 1. DTO trả về thông tin Mentor trong danh sách
    public class MentorDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    // 2. DTO trả về thông tin Thực tập sinh trong danh sách
    public class InternDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Mentor { get; set; }
        public string? MentorEmail { get; set; }
    }

    // 3. DTO nhận dữ liệu từ Frontend gửi lên để lưu phân công
    public class AssignMentorRequest
    {
        public int MentorId { get; set; }
        public List<int> InternIds { get; set; } = new();
    }
}