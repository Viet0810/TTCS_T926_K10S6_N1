using InternManagement.Models;

namespace InternManagement.Repositories
{
    public static class InMemoryUserStore
    {
        public static List<User> Users = new List<User>
        {
            new User
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                FullName = "System Admin",
                Email = "admin@gmail.com.vn",
                PasswordHash = "Admin5678@",
                Role = "ADMIN",
                CreatedAt = DateTime.UtcNow
            }
        };
    }
}