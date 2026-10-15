
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using InternManagement.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

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

// Service quản lý thời gian chương trình
builder.Services.AddScoped<InternManagement.Services.ProgramScheduleService>();

// Service phân công thực tập
builder.Services.AddScoped<InternManagement.Services.InternAssignmentService>();
builder.Services.AddScoped<InternManagement.Services.InternTaskService>();

// K10S6N1-128: Service lấy và tổng hợp đánh giá cuối kỳ
builder.Services.AddScoped<InternManagement.Services.FinalEvaluationService>();

// Các Service hiện có
builder.Services.AddScoped<InternManagement.Services.ContractService>();
builder.Services.AddScoped<InternManagement.Services.OwnAttendanceService>();

builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);

builder.Services.AddSingleton<InternManagement.Services.DatabaseInitializer>();
builder.Services.AddScoped<InternManagement.Services.ListPagingService>();

builder.Services.AddScoped<
    InternManagement.Services.IEmailService,
    InternManagement.Services.EmailService
>();

builder.Services.AddScoped<InternManagement.Services.PasswordResetService>();
builder.Services.AddScoped<InternManagement.Services.MeetingService>();

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy(
        "password-recovery",
        context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(15),
                    QueueLimit = 0
                }
            )
    );

    options.OnRejected = async (
        context,
        cancellationToken
    ) =>
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

builder.Services.AddScoped<
    InternManagement.Services.INotificationService,
    InternManagement.Services.NotificationService
>();

builder.Services.AddScoped<
    InternManagement.Services.ILeaveRequestService,
    InternManagement.Services.LeaveRequestService
>();

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
}

var connectionString =
    builder.Configuration.GetConnectionString("InternManagement");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Chưa cấu hình ConnectionStrings:InternManagement."
    );
}

await app.Services
    .GetRequiredService<InternManagement.Services.DatabaseInitializer>()
    .InitializeAsync(connectionString);

app.UseMiddleware<ApiRequestMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors();

app.UseRateLimiter();

app.MapControllers();

app.MapGet(
    "/api/database/status",
    async () =>
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return Results.Problem(
                "Chưa cấu hình connection string InternManagement.",
                statusCode: 500
            );
        }

        try
        {
            await using var connection =
                new Microsoft.Data.SqlClient.SqlConnection(
                    connectionString
                );

            await connection.OpenAsync();

            await using var command = connection.CreateCommand();

            command.CommandText = "SELECT 1";

            await using var reader =
                await command.ExecuteReaderAsync();

            await reader.ReadAsync();

            return Results.Ok(
                new
                {
                    connected = true
                }
            );
        }
        catch (Exception error)
        {
            app.Logger.LogError(
                error,
                "Database health check failed."
            );

            return Results.Problem(
                "Không thể kết nối cơ sở dữ liệu. Vui lòng thử lại sau.",
                statusCode: 503
            );
        }
    }
);

// Giữ nguyên endpoint weatherforecast hiện có
var summaries = new[]
{
    "Freezing",
    "Bracing",
    "Chilly",
    "Cool",
    "Mild",
    "Warm",
    "Balmy",
    "Hot",
    "Sweltering",
    "Scorching"
};

app.MapGet(
    "/weatherforecast",
    () => Enumerable.Range(1, 5)
        .Select(index =>
            new WeatherForecast(
                DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                Random.Shared.Next(-20, 55),
                summaries[Random.Shared.Next(summaries.Length)]
            )
        )
        .ToArray()
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
