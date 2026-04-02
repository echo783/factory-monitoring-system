using OpenCvSharp;

namespace FactoryApi.Infrastructure.CameraRuntime
{
    public sealed class SnapshotFileService
    {
        private readonly string _basePath;
        private readonly ILogger<SnapshotFileService> _logger;

        public SnapshotFileService(IWebHostEnvironment env, ILogger<SnapshotFileService> logger)
        {
            _basePath = Path.Combine(env.ContentRootPath, "captures");
            _logger = logger;
        }

        // 📌 1. 최신 이미지 저장 (latest.jpg)
        public async Task SaveLatestAsync(int cameraId, Mat frame, CancellationToken token)
        {
            try
            {
                string dir = GetCameraDir(cameraId);
                Directory.CreateDirectory(dir);

                string path = Path.Combine(dir, "latest.jpg");

                var jpegParams = new[]
                {
                    new ImageEncodingParam(ImwriteFlags.JpegQuality, 75)
                };

                // 메모리 인코딩 후 저장 (파일락 방지)
                Cv2.ImEncode(".jpg", frame, out var bytes, jpegParams);

                await File.WriteAllBytesAsync(path, bytes, token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SaveLatestAsync error. CameraId={CameraId}", cameraId);
            }
        }

        // 📌 2. 이벤트 스냅샷 저장 (카운트 발생 시)
        public async Task<string?> SaveEventSnapshotAsync(
            int cameraId,
            Mat frame,
            DateTime capturedAt,
            CancellationToken token)
        {
            try
            {
                string dir = GetCameraDir(cameraId);
                Directory.CreateDirectory(dir);

                string fileName = $"event_{capturedAt:yyyyMMdd_HHmmss_fff}.jpg";
                string path = Path.Combine(dir, fileName);

                var jpegParams = new[]
                {
                    new ImageEncodingParam(ImwriteFlags.JpegQuality, 90)
                };

                Cv2.ImEncode(".jpg", frame, out var bytes, jpegParams);

                await File.WriteAllBytesAsync(path, bytes, token);

                return path;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SaveEventSnapshotAsync error. CameraId={CameraId}", cameraId);
                return null;
            }
        }

        private string GetCameraDir(int cameraId)
        {
            return Path.Combine(_basePath, $"cam{cameraId}");
        }
    }
}