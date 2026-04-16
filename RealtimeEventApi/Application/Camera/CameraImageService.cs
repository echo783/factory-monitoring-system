using FactoryApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using RealtimeEventApi.Application.Camera.Dtos;

namespace FactoryApi.Application.Camera
{
    public class CameraImageService
    {
        private readonly FactoryDbContext _context;
        private readonly IWebHostEnvironment _env;

        public CameraImageService(FactoryDbContext context, IWebHostEnvironment env) 
        {
             _context = context;
            _env = env;
        }

        public async Task<CameraImageResult> GetImageAsync(int cameraId, CancellationToken ct) 
        {
           
            var result = new CameraImageResult();

            result.CameraExists = await _context.CameraConfigs
               .AnyAsync(x => x.CameraId == cameraId, ct);

            if (!result.CameraExists)
            {
                result.ImageExists = false;
                result.Bytes = null;
                return result;
            }

            string imagePath = Path.Combine(
                _env.ContentRootPath,
                "captures",
                $"cam{cameraId}",
                "latest.jpg");

            bool imagePathExists = File.Exists(imagePath);
            result.ImageExists = imagePathExists;

            if (imagePathExists)
            {
                result.Bytes = await ReadFileSafeAsync(imagePath, ct);
            }

            return result;
        }


        private static async Task<byte[]> ReadFileSafeAsync(string path, CancellationToken ct)
        {
            const int maxRetry = 3;

            for (int i = 0; i < maxRetry; i++)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    await using var fs = new FileStream(
                        path,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete,
                        81920,
                        useAsync: true);

                    var bytes = new byte[fs.Length];
                    int offset = 0;

                    while (offset < bytes.Length)
                    {
                        ct.ThrowIfCancellationRequested();

                        int read = await fs.ReadAsync(
                            bytes.AsMemory(offset, bytes.Length - offset), ct);

                        if (read == 0)
                            break;

                        offset += read;
                    }

                    if (offset == bytes.Length)
                        return bytes;

                    return bytes.Take(offset).ToArray();
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (IOException) when (i < maxRetry - 1)
                {
                    await Task.Delay(30, ct);
                }
            }

            throw new IOException("latest 이미지 파일을 안정적으로 읽지 못했습니다.");
        }

    }
}
