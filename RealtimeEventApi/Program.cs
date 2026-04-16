using FactoryApi.Application.Ai;
using FactoryApi.Hubs;
using FactoryApi.Infrastructure.Ai;
using FactoryApi.Infrastructure.DependencyInjection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddAuthServices(builder.Configuration);

builder.Services.AddCameraApplication();
builder.Services.AddCameraRuntime();
builder.Services.AddMonitor();

//python vision client 설정
builder.Services.AddAiServices(builder.Configuration);

builder.Services.AddMediaMtx(builder.Configuration);

builder.Services.AddSignalR();
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();


// ======================================================
// Development only
// ======================================================
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Swagger UI가 필요해지면 아래 2줄 활성화
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.MapHub<CameraHub>("/hubs/camera");

var logger = app.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("RequestTrace");

var cs = builder.Configuration.GetConnectionString("DefaultConnection");
Console.WriteLine("=== DefaultConnection ===");
Console.WriteLine(cs);

app.Use(async (context, next) =>
{
    Console.WriteLine($"[REQ] {context.Request.Method} {context.Request.Path}");

    await next();

    Console.WriteLine($"[RES] {context.Response.StatusCode} {context.Request.Path}");
});

// ======================================================
// Middleware
// ======================================================
//app.UseHttpsRedirection();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
 
app.Run();