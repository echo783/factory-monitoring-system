using FactoryApi.Infrastructure.Persistence;
using FactoryApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text;

namespace FactoryApi.Infrastructure.MediaMtx
{
    public sealed class MediaMtxConfigWriter
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MediaMtxConfigWriter> _logger;
        private readonly MediaMtxOptions _options;

        public MediaMtxConfigWriter(
            IServiceScopeFactory scopeFactory,
            ILogger<MediaMtxConfigWriter> logger,
            IOptions<MediaMtxOptions> options)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _options = options.Value;
        }

        public async Task<string> WriteConfigAsync(CancellationToken token = default)
        {
            using var scope = _scopeFactory.CreateScope();

            var db = scope.ServiceProvider.GetRequiredService<FactoryDbContext>();

            var cameras = await db.Set<CameraConfig>()
                .AsNoTracking()
                .Where(x => x.Enabled)
                .OrderBy(x => x.CameraId)
                .ToListAsync(token);

            string baseDir = AppContext.BaseDirectory;
            string exePath = Path.GetFullPath(Path.Combine(baseDir, _options.ExeRelativePath));
            string mtxDir = Path.GetDirectoryName(exePath) ?? baseDir;
            Directory.CreateDirectory(mtxDir);

            string configPath = Path.GetFullPath(Path.Combine(baseDir, _options.ConfigRelativePath));

            string yaml = BuildYaml(cameras);

            await File.WriteAllTextAsync(configPath, yaml, new UTF8Encoding(false), token);

            _logger.LogInformation("MediaMTX config written. Count={Count}, Path={Path}",
                cameras.Count, configPath);

            return configPath;
        }

        private string BuildYaml(List<CameraConfig> cameras)
        {
            var sb = new StringBuilder();

            sb.AppendLine("logLevel: info");
            sb.AppendLine("logDestinations: [stdout]");
            sb.AppendLine();
            sb.AppendLine("rtsp: true");
            sb.AppendLine("rtspAddress: :8554");
            sb.AppendLine();
            sb.AppendLine("hls: false");
            sb.AppendLine("webrtc: false");
            sb.AppendLine("api: false");
            sb.AppendLine("metrics: false");
            sb.AppendLine("pprof: false");
            sb.AppendLine();
            sb.AppendLine("paths:");

            if (cameras.Count == 0)
            {
                sb.AppendLine("  dummy:");
                sb.AppendLine("    runOnInit: echo no_camera_configured");
                return sb.ToString();
            }

            foreach (var cam in cameras)
            {
                string pathName = NormalizePathName(cam);
                string source = EscapeYaml(cam.RtspUrl);

                sb.AppendLine($"  {pathName}:");
                sb.AppendLine($"    source: {source}");
                sb.AppendLine("    sourceOnDemand: no");
                sb.AppendLine("    rtspTransport: tcp");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static string NormalizePathName(CameraConfig cam)
        {
            string path = $"cam{cam.CameraId}";

            if (string.IsNullOrWhiteSpace(path))
                path = $"cam{cam.CameraId}";

            path = path.Replace("\\", "/").Trim('/');
            return path;
        }

        private static string EscapeYaml(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "\"\"";

            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }
}