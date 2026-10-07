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

// THÊM SERVICE QUẢN LÝ THỜI GIAN CHƯƠNG TRÌNH
builder.Services.AddScoped<InternManagement.Services.ProgramScheduleService>();

builder.Services.AddSingleton<InternManagement.Services.DatabaseInitializer>();
builder.Services.AddScoped<InternManagement.Services.PasswordResetService>();

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


var app = builder.Build();

var connectionString =
    builder.Configuration.GetConnectionString(
        "InternManagement"
    );

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Chưa cấu hình ConnectionStrings:InternManagement."
    );
}

await app.Services
    .GetRequiredService<
        InternManagement.Services.DatabaseInitializer
    >()
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
        if (
            string.IsNullOrWhiteSpace(
                connectionString
            )
        )
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

            await using var command =
                connection.CreateCommand();

            command.CommandText =
                "SELECT DB_NAME() AS DatabaseName, SUSER_SNAME() AS LoginName";

            await using var reader =
                await command.ExecuteReaderAsync();

            await reader.ReadAsync();

            return Results.Ok(
                new
                {
                    connected = true,
                    database =
                        reader.GetString(0),
                    login =
                        reader.GetString(1)
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

app.Run();