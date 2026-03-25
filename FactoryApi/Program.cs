using FactoryApi.Data;
using FactoryApi.Repositories;
using FactoryApi.Services;
using FactoryApi.Services.CameraRuntime;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);


// ======================================================
// Controllers
// ======================================================
builder.Services.AddControllers();


// ======================================================
// Database
// ======================================================
builder.Services.AddDbContext<FactoryDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


// ======================================================
// Dapper / Repository
// ======================================================
builder.Services.AddSingleton<SqlConnectionFactory>();
builder.Services.AddScoped<DeliveryRepository>();


// ======================================================
// Logging
// ======================================================
// 로그 파일 경로: 바탕화면\logviewer.txt
//var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
//var logPath = Path.Combine(desktopPath, "logviewer.txt");

//// 앱 시작 시 로그 초기화
//File.WriteAllText(
//    logPath,
//    $"=== FactoryApi START {DateTime.Now:yyyy-MM-dd HH:mm:ss} ==={Environment.NewLine}");

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// 파일 로그는 필요 시 다시 활성화
//builder.Logging.AddProvider(new SimpleFileLoggerProvider(logPath));

builder.Logging.SetMinimumLevel(LogLevel.Information);


// ======================================================
// OpenAPI / Swagger / https://localhost:7125/swagger
// ======================================================
builder.Services.AddOpenApi();

// Swagger UI가 필요해지면 아래 2줄 활성화
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


// ======================================================
// MediaMTX
// ======================================================
builder.Services.Configure<MediaMtxOptions>(
    builder.Configuration.GetSection("MediaMtx"));

builder.Services.AddSingleton<MediaMtxConfigWriter>();
builder.Services.AddHostedService<MediaMtxService>();


// ======================================================
// Camera Runtime
// ======================================================
builder.Services.AddSingleton<SnapshotFileService>();
builder.Services.AddSingleton<ProductionPersistenceService>();
builder.Services.AddSingleton<CameraOrchestrator>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<CameraOrchestrator>());
builder.Services.AddSingleton<ILabelDetector, DummyLabelDetector>();


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


// ======================================================
// Middleware
// ======================================================
app.UseHttpsRedirection();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthorization();

app.MapControllers();

app.Run();