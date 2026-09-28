var builder = WebApplication.CreateBuilder(args);

// 1. Đăng ký Web API Controllers
builder.Services.AddControllers();

// 2. Cấu hình CORS để kết nối với Frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// 3. Kích hoạt CORS
app.UseCors("AllowFrontend");

app.UseAuthorization();

// 4. Map các Endpoint Controllers (Auth & Users)
app.MapControllers();

app.Run();