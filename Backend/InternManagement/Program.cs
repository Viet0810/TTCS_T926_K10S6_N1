var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddDataProtection();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddSingleton<InternManagement.Services.PasswordHasher>();
builder.Services.AddSingleton<InternManagement.Services.AuthTokenService>();
builder.Services.AddSingleton<InternManagement.Services.RolePermissionService>();
builder.Services.AddScoped<InternManagement.Services.RequestAuthorizationService>();
builder.Services.AddSingleton<InternManagement.Services.DatabaseInitializer>();
builder.Services.AddScoped<InternManagement.Services.IInternService, InternManagement.Services.InternService>();
builder.Services.AddScoped<InternManagement.HrSearchFilterInterns.IInternFilterService, InternManagement.HrSearchFilterInterns.InternFilterService>();

var app = builder.Build();
var connectionString = builder.Configuration.GetConnectionString("InternManagement");

if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Chưa cấu hình ConnectionStrings:InternManagement.");

await app.Services.GetRequiredService<InternManagement.Services.DatabaseInitializer>()
    .InitializeAsync(connectionString);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseCors();
app.MapControllers();

app.MapGet("/api/database/status", async () =>
{
    if (string.IsNullOrWhiteSpace(connectionString))
        return Results.Problem("Chưa cấu hình connection string InternManagement.", statusCode: 500);

    try
    {
        await using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT DB_NAME() AS DatabaseName, SUSER_SNAME() AS LoginName";
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return Results.Ok(new
        {
            connected = true,
            database = reader.GetString(0),
            login = reader.GetString(1)
        });
    }
    catch (Exception error)
    {
        return Results.Problem($"Không thể kết nối SQL Server: {error.Message}", statusCode: 503);
    }
});

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
