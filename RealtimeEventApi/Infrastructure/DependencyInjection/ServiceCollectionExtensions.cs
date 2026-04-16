using FactoryApi.Application.Ai;
using FactoryApi.Application.Camera;
using FactoryApi.Application.Monitor;
using FactoryApi.Infrastructure.Ai;
using FactoryApi.Infrastructure.Auth;
using FactoryApi.Infrastructure.CameraRuntime;
using FactoryApi.Infrastructure.MediaMtx;
using FactoryApi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RealtimeEventApi.Application.Camera;
using System.Text;

namespace FactoryApi.Infrastructure.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration config)
        {
            services.AddDbContext<FactoryDbContext>(options =>
                options.UseSqlServer(config.GetConnectionString("DefaultConnection")));

            services.AddSingleton<SqlConnectionFactory>();
            services.AddScoped<DeliveryRepository>();

            return services;
        }

        public static IServiceCollection AddAuthServices(this IServiceCollection services, IConfiguration config)
        {
            services.AddScoped<JwtTokenService>();

            var jwtKey = config["Jwt:Key"]!;
            var jwtIssuer = config["Jwt:Issuer"]!;
            var jwtAudience = config["Jwt:Audience"]!;

            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwtIssuer,
                        ValidateAudience = true,
                        ValidAudience = jwtAudience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero
                    };
                });

            services.AddAuthorization();

            return services;
        }

        public static IServiceCollection AddCameraApplication(this IServiceCollection services)
        {
            services.AddScoped<CameraQueryService>();
            services.AddScoped<CameraDebugService>();
            services.AddScoped<CameraImageService>();
            services.AddScoped<CameraRoiService>();
            services.AddScoped<CameraCommandService>();

            return services;
        }

        public static IServiceCollection AddCameraRuntime(this IServiceCollection services)
        {
            services.AddSingleton<SnapshotFileService>();
            services.AddSingleton<ProductionPersistenceService>();
            services.AddSingleton<CameraOrchestrator>();
            services.AddSingleton<ICameraRuntimeReader>(sp => sp.GetRequiredService<CameraOrchestrator>());
            services.AddSingleton<ILabelDetector, DummyLabelDetector>();

            return services;
        }

        public static IServiceCollection AddMonitor(this IServiceCollection services)
        {
            services.AddScoped<MonitorQueryService>();
            return services;
        }

        public static IServiceCollection AddMediaMtx(this IServiceCollection services, IConfiguration config)
        {
            services.Configure<MediaMtxOptions>(
                config.GetSection("MediaMtx"));

            services.AddSingleton<MediaMtxConfigWriter>();

            return services;
        }

        public static IServiceCollection AddAiServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var pythonBaseUrl = configuration["Ai:PythonBaseUrl"]
                                ?? "http://127.0.0.1:8000";

            services.AddHttpClient<PythonVisionClient>(client =>
            {
                client.BaseAddress = new Uri(pythonBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(15);
            });

            services.AddScoped<CameraRoiValidationService>();

            return services;
        }
    }
}