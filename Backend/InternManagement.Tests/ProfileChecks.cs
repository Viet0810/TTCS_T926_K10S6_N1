using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using InternManagement.DTOs;
using InternManagement.Services;
using Microsoft.Extensions.Configuration;

internal static class ProfileChecks
{
    public static async Task RunAsync(IConfiguration configuration, Action<bool, string> check)
    {
        var service = new InternService(configuration);
        var request = new CreateInternRequest {
            FullName="Profile roundtrip", Email="extended@example.invalid", Phone="0900000001", School="School", Major="Major",
            StudentCode="PROFILE-2026", ClassName="K10", Faculty="Faculty", DateOfBirth=new(2004,1,2), Address="Contact address",
            Organization="Organization", OrganizationAddress="Company address", Department="Engineering", Position="Intern",
            Mentor="Supervisor", MentorEmail="mentor@example.invalid", MentorPhone="0900000002", AcademicSupervisor="Lecturer",
            StartDate=new(2026,1,2), EndDate=new(2026,6,30), Status="Đang thực tập", InternshipTopic="Project topic", Notes="Unicode ghi chú\nSecond line"
        };
        var created = await service.CreateAsync(request, default);
        bool Matches(InternResponse? response, CreateInternRequest expected) {
            if (response is null) return false;
            var actual = JsonSerializer.SerializeToElement(response);
            return JsonSerializer.SerializeToElement(expected).EnumerateObject().All(field => actual.GetProperty(field.Name).ToString()==field.Value.ToString());
        }
        check(Matches(created, request), "all 23 profile fields roundtrip through SQL INSERT");
        var changed = request with {Notes="Updated notes", EndDate=new(2026,7,1), Status="Đã hoàn thành"};
        check(Matches(await service.UpdateAsync(created.Id, changed, default), changed), "all profile fields roundtrip through SQL UPDATE");
        await new DatabaseInitializer().InitializeAsync(configuration.GetConnectionString("InternManagement")!);
        await new DatabaseInitializer().InitializeAsync(configuration.GetConnectionString("InternManagement")!);
        check(Matches(await service.GetByIdAsync(created.Id, default), changed), "repeated schema migrations preserve populated profile fields");
        var found = await new InternFilterService(configuration).FilterInternsAsync(new InternFilterRequest {Search=request.StudentCode}, default);
        check(found.Count==1 && Matches(found[0], changed), "student code search returns the complete profile");
        bool Valid(CreateInternRequest value) => Validator.TryValidateObject(value, new ValidationContext(value), new List<ValidationResult>(), true);
        check(Valid(request), "complete profile passes validation");
        check(!Valid(request with {EndDate=new(2025,1,1)}), "end date before start is rejected");
        check(!Valid(request with {DateOfBirth=new(2099,1,1)}), "future birth date is rejected");
        check(!Valid(request with {StartDate=new(2000,1,1)}), "internship before birth is rejected");
        check(!Valid(request with {MentorEmail="invalid"}) && !Valid(request with {MentorPhone="abc"}), "invalid mentor contact details are rejected");
        check(!Valid(request with {Status="Unknown"}) && !Valid(request with {Notes=new string('x',2001)}), "unsupported status and oversized notes are rejected");
    }
}
