using System.Reflection;
using InternManagement.Controllers;
using InternManagement.Models;
using InternManagement.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InternManagement.Tests;

public class InternPortalBusinessRulesTests
{
    [Fact]
    public void RolePermissions_ForIntern_ReturnsTaskAndProfilePermissions()
    {
        var permissions = RolePermissions.ForRole("INTERN");

        Assert.Contains("profile.own.read", permissions);
        Assert.Contains("progress.own.read", permissions);
        Assert.Contains("tasks.own.read", permissions);
    }

    [Fact]
    public void AuthTokenService_IssueAndValidate_RoundTripsUser()
    {
        var services = new ServiceCollection();
        services.AddDataProtection();
        using var provider = services.BuildServiceProvider();

        var tokenService = new AuthTokenService(provider.GetRequiredService<IDataProtectionProvider>());
        var user = new User
        {
            Id = 42,
            Username = "intern.demo",
            FullName = "Intern Demo",
            Email = "intern.demo@example.com",
            Role = "INTERN"
        };

        var token = tokenService.Issue(user);

        Assert.True(tokenService.TryValidate(token, out var authenticated));
        Assert.NotNull(authenticated);
        Assert.Equal(42, authenticated!.Id);
        Assert.Equal("intern.demo", authenticated.Username);
        Assert.Equal("INTERN", authenticated.Role);
    }

    [Fact]
    public void TaskStatusTransition_AllowsNotStartedToInProgress()
    {
        var allowed = InternPortalController.IsAllowedTaskStatusTransition("NotStarted", "InProgress", out var message);

        Assert.True(allowed);
        Assert.Null(message);
    }

    [Fact]
    public void TaskStatusTransition_RejectsInvalidFlowAndCompletedTask()
    {
        var invalid = InternPortalController.IsAllowedTaskStatusTransition("NotStarted", "PendingReview", out var invalidMessage);
        Assert.False(invalid);
        Assert.Equal("Nhiệm vụ phải ở trạng thái Đang thực hiện trước khi gửi đánh giá.", invalidMessage);

        var completed = InternPortalController.IsAllowedTaskStatusTransition("Completed", "InProgress", out var completedMessage);
        Assert.False(completed);
        Assert.Equal("Nhiệm vụ đã hoàn thành, không thể cập nhật tiếp.", completedMessage);
    }

    [Fact]
    public void OwnershipRule_RejectsCrossInternAccess()
    {
        const int internAUserId = 101;
        const int internBUserId = 202;

        Assert.True(InternPortalController.IsTaskOwnedByCurrentIntern(internAUserId, internAUserId));
        Assert.False(InternPortalController.IsTaskOwnedByCurrentIntern(internAUserId, internBUserId));
        Assert.False(InternPortalController.IsTaskOwnedByCurrentIntern(internBUserId, internAUserId));
    }

    [Fact]
    public void TryIntern_RejectsNonInternRole()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:InternManagement"] = "Server=(localdb)\\MSSQLLocalDB;Database=InternManagement;Trusted_Connection=True;"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddDataProtection();
        using var provider = services.BuildServiceProvider();

        var tokenService = new AuthTokenService(provider.GetRequiredService<IDataProtectionProvider>());
        var controller = new InternPortalController(config, tokenService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        controller.ControllerContext.HttpContext.Request.Headers.Authorization =
            $"Bearer {tokenService.Issue(new User { Id = 7, Username = "mentor.demo", FullName = "Mentor Demo", Email = "mentor.demo@example.com", Role = "MENTOR" })}";

        var method = typeof(InternPortalController).GetMethod("TryIntern", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var args = new object?[] { null, null };
        var result = (bool)method.Invoke(controller, args)!;

        Assert.False(result);
        Assert.NotNull(args[1]);
        var objectResult = Assert.IsType<ObjectResult>(args[1]);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }
}
