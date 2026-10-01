using InternManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace InternManagement.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var user = modelBuilder.Entity<AppUser>();
        user.ToTable("Users");
        user.HasKey(item => item.Id);
        user.Property(item => item.FullName).HasMaxLength(150).IsRequired();
        user.Property(item => item.Email).HasMaxLength(256).IsRequired();
        user.HasIndex(item => item.Email).IsUnique();
        user.Property(item => item.UserName).HasMaxLength(256).IsRequired();
        user.HasIndex(item => item.UserName).IsUnique();
        user.Property(item => item.PasswordHash).HasMaxLength(500).IsRequired();
        user.Property(item => item.Role).HasMaxLength(20).IsRequired();
    }
}
