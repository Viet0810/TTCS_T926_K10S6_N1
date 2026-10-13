using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using InternManagement.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// BỔ SUNG DÒNG NÀY ĐỂ FIX LỖI CS0103 (Không tìm thấy connectionString)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                       ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Logging.AddSimpleConsole(options =>
{
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss 'UTC' ";
    options.UseUtcTimestamp = true;
});

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddDataProtection();

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("X-Request-ID")
    )
);

builder.Services.AddSingleton<InternManagement.Services.PasswordHasher>();
builder.Services.AddSingleton<InternManagement.Services.AuthTokenService>();
builder.Services.AddSingleton<InternManagement.Services.RolePermissionService>();

builder.Services.AddScoped<InternManagement.Services.RequestAuthorizationService>();
builder.Services.AddScoped<InternManagement.Services.AccountService>();
builder.Services.AddScoped<InternManagement.Services.InternDocumentService>();

// Các service hiện có của dự án
builder.Services.AddScoped<InternManagement.Services.ProgramScheduleService>();
builder.Services.AddScoped<InternManagement.Services.InternAssignmentService>();
builder.Services.AddScoped<InternManagement.Services.ContractService>();
builder.Services.AddScoped<InternManagement.Services.OwnAttendanceService>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);

builder.Services.AddScoped<InternManagement.Services.ListPagingService>();
builder.Services.AddScoped<
    InternManagement.Services.IEmailService,
    InternManagement.Services.EmailService
>();
builder.Services.AddScoped<InternManagement.Services.PasswordResetService>();

// Service quản lý lịch làm việc thực tập sinh
builder.Services.AddSingleton<
    InternManagement.Services.IInternWorkScheduleService,
    InternManagement.Services.InternWorkScheduleService
>();

builder.Services.AddScoped<
    InternManagement.Services.IInternService,
    InternManagement.Services.InternService
>();

builder.Services.AddScoped<
    InternManagement.Services.IInternFilterService,
    InternManagement.Services.InternFilterService
>();

builder.Services.AddScoped<
    InternManagement.Services.IAttendanceReportService,
    InternManagement.Services.AttendanceReportService
>();

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy(
        "password-recovery",
        context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(15),
                    QueueLimit = 0
                }
            )
    );

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode =
            StatusCodes.Status429TooManyRequests;

        await context.HttpContext.Response.WriteAsJsonAsync(
            new
            {
                message =
                    "Bạn đã gửi quá nhiều yêu cầu. Vui lòng thử lại sau 15 phút."
            },
            cancellationToken
        );
    };
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    var smtpSettings = builder.Configuration.GetSection("Smtp");

    app.Logger.LogInformation(
        "SMTP configuration: Host {Host}, UsernameConfigured {UsernameConfigured}, PasswordConfigured {PasswordConfigured}, SenderConfigured {SenderConfigured}.",
        smtpSettings["Host"],
        !string.IsNullOrWhiteSpace(smtpSettings["Username"]),
        !string.IsNullOrWhiteSpace(smtpSettings["Password"]),
        !string.IsNullOrWhiteSpace(smtpSettings["FromAddress"])
    );

    app.MapOpenApi();
}

app.UseMiddleware<ApiRequestMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors();
app.UseRateLimiter();

app.MapControllers();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild",
    "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet(
    "/weatherforecast",
    () => Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast(
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        )
    ).ToArray()
).WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(
    DateOnly Date,
    int TemperatureC,
    string? Summary
)
{
    public int TemperatureF =>
        32 + (int)(TemperatureC / 0.5556);
}