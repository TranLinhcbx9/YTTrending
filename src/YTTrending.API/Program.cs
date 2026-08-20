using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;   // MigrateAsync
using Serilog;                         // UseSerilog / UseSerilogRequestLogging
using YTTrending.Application.Common.Interfaces;   // IChannelRepository / IUnitOfWork cho seed

var builder = WebApplication.CreateBuilder(args);

const string CorsPolicy = "AngularDev";   // tên policy CORS, dùng lại 2 nơi

// Thay logger mặc định bằng Serilog, đọc cấu hình từ section "Serilog"
builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

builder.Services
    .AddControllers()
    // Enum trả về string ("Tracking") thay vì số, cho Angular dễ đọc
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();   // Swagger quét được endpoint
builder.Services.AddSwaggerGen();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();   // bắt lỗi chưa xử lý -> 500 JSON gọn
builder.Services.AddProblemDetails();                             // UseExceptionHandler() rỗng cần dịch vụ này

// Cho Angular (khác origin) gọi API; danh sách origin đọc từ config, không hardcode
builder.Services.AddCors(o => o.AddPolicy(CorsPolicy, p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddApplication(builder.Configuration);      // MediatR + behaviors + ValidateOnStart (mắt xích hở)
builder.Services.AddInfrastructure(builder.Configuration);   // DbContext + TimeProvider

var app = builder.Build();

// Tự migrate DB lúc khởi động nếu bật cờ; dùng MigrateAsync, TUYỆT ĐỐI không EnsureCreated
if (app.Configuration.GetValue<bool>("Database:AutoMigrate"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<YTTrendingDbContext>();
    await db.Database.MigrateAsync();

    // Seed dữ liệu giả chỉ ở Development, sau khi migrate xong — qua Repository, không đụng DbContext thẳng
    if (app.Environment.IsDevelopment())
    {
        var channels = scope.ServiceProvider.GetRequiredService<IChannelRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await DevDataSeeder.SeedAsync(channels, uow);
    }
}

app.UseExceptionHandler();          // đặt đầu pipeline để gom mọi lỗi phía sau
app.UseSerilogRequestLogging();     // ghi 1 dòng log gọn cho mỗi request

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors(CorsPolicy);        // CORS phải chạy TRƯỚC MapControllers
}

app.MapControllers();

app.Run();
