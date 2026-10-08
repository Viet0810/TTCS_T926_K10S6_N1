using System.ComponentModel.DataAnnotations;
using InternManagement.DTOs;
using InternManagement.Controllers;
using InternManagement.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

internal static class Sprint2ValidationChecks
{
    public static async Task RunAsync()
    {
        static bool Valid(object value) => Validator.TryValidateObject(value, new ValidationContext(value), [], true);
        static void Check(bool value, string name)
        {
            if (!value) throw new Exception("FAIL: " + name);
            Console.WriteLine("PASS: " + name);
        }
        var registration = new RegisterInternRequest { FullName="Test", Email="test@gmail.com", Password="Abc@1234", Phone="0912345678", School="Test", Major="Test" };
        Check(Valid(registration) && Valid(registration with { Email="dtc245200682@ictu.edu.vn" }), "registration accepts Gmail and ICTU");
        foreach (var email in new[] { "abc@yahoo.com", "abc@gmail", "abc@ictu.com", "@gmail.com" })
            Check(!Valid(registration with { Email=email }), "registration rejects " + email);
        foreach (var password in new[] { "short", "abc@1234", "ABC@1234", "Abc@abcd", "Abcd1234" })
            Check(!Valid(registration with { Password=password }), "registration rejects weak password");
        foreach (var phone in new[] { "0123456789", "091234567", "09123456789", "091abc5678" })
            Check(!Valid(registration with { Phone=phone }), "registration rejects invalid phone");
        Check(Valid(new AttendanceReportRequest { StartDate="2026-10-01", EndDate="2026-10-08", Status="LATE" }), "report accepts valid filters");
        Check(!Valid(new AttendanceReportRequest { StartDate="invalid" }) && !Valid(new AttendanceReportRequest { StartDate="2026-10-08", EndDate="2026-10-01" }) && !Valid(new AttendanceReportRequest { Status="UNKNOWN" }), "report rejects invalid date range and status");
        var record = new CreateAttendanceRecordRequest { InternId=1, Date="2026-10-08", CheckIn="08:00", CheckOut="17:00", Hours=8, Status="ON_TIME" };
        Check(Valid(record), "attendance accepts a valid record");
        Check(!Valid(record with { InternId=0 }) && !Valid(record with { Hours=-1 }) && !Valid(record with { CheckOut="07:00" }) && !Valid(record with { Date="2026-99-99" }), "attendance rejects invalid intern, hours, chronology and date");
        Check(Valid(record with { Status="LEAVE_APPROVED", CheckIn="—", CheckOut="—", Hours=0 }), "attendance keeps leave-record compatibility");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:InternManagement"]="Server=localhost;Database=Unused;Integrated Security=true"
        }).Build();
        var authorization = new RequestAuthorizationService(new AuthTokenService(new EphemeralDataProtectionProvider(), config), new RolePermissionService());
        var controller = new ProgramScheduleController(new ProgramScheduleService(config), authorization)
        {
            ControllerContext = new ControllerContext { HttpContext=new DefaultHttpContext() }
        };
        Check((await controller.GetAll() as ObjectResult)?.StatusCode == 401
            && (await controller.Create(new ProgramScheduleRequest()) as ObjectResult)?.StatusCode == 401
            && (await controller.Delete(1) as ObjectResult)?.StatusCode == 401,
            "program list/create/delete reject unauthenticated requests before SQL access");
        var attendance = new AttendanceReportController(new AttendanceReportService(config), authorization)
        {
            ControllerContext = new ControllerContext { HttpContext=new DefaultHttpContext() }
        };
        Check((await attendance.GetReport(null, default) as ObjectResult)?.StatusCode == 401
            && (await attendance.RecordAttendance(record, default) as ObjectResult)?.StatusCode == 401,
            "attendance report and recording reject unauthenticated requests before SQL access");
    }
}
