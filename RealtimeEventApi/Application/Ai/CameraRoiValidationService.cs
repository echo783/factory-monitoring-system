using FactoryApi.Infrastructure.Ai;
using FactoryApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace FactoryApi.Application.Ai
{
    public sealed class CameraRoiValidationService
    {
        private readonly FactoryDbContext _context;
        private readonly PythonVisionClient _pythonVisionClient;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<CameraRoiValidationService> _logger;

        public CameraRoiValidationService(
            FactoryDbContext context,
            PythonVisionClient pythonVisionClient,
            IWebHostEnvironment environment,
            ILogger<CameraRoiValidationService> logger)
        {
            _context = context;
            _pythonVisionClient = pythonVisionClient;
            _environment = environment;
            _logger = logger;
        }

        public async Task<CameraRoiValidationResult> ValidateAsync(
            int cameraId,
            CancellationToken ct)
        {
            var result = new CameraRoiValidationResult();

            var cam = await _context.CameraConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.CameraId == cameraId, ct);

            if (cam == null)
            {
                result.CameraExists = false;
                result.Success = false;
                result.Message = $"CameraId={cameraId} 카메라를 찾을 수 없습니다.";
                return result;
            }

            result.CameraExists = true;

            var imagePath = Path.Combine(
                _environment.ContentRootPath,
                "captures",
                $"cam{cameraId}",
                "latest.jpg");

            result.ImagePath = imagePath;

            if (!File.Exists(imagePath))
            {
                result.ImageExists = false;
                result.Success = false;
                result.Message = "최신 이미지가 존재하지 않습니다.";
                return result;
            }

            result.ImageExists = true;

            var request = new PythonValidateRoiRequest
            {
                ImagePath = imagePath,

                ObjectX = cam.ObjectRoiX,
                ObjectY = cam.ObjectRoiY,
                ObjectW = cam.ObjectRoiW,
                ObjectH = cam.ObjectRoiH,

                LabelX = cam.LabelRoiX,
                LabelY = cam.LabelRoiY,
                LabelW = cam.LabelRoiW,
                LabelH = cam.LabelRoiH
            };

            var pyResult = await _pythonVisionClient.ValidateRoiAsync(request, ct);

            result.Success = pyResult.Success;
            result.ObjectDetected = pyResult.ObjectDetected;
            result.ObjectConfidence = pyResult.ObjectConfidence;
            result.ObjectCount = pyResult.ObjectCount;
            result.ObjectClasses = pyResult.ObjectClasses ?? new();

            result.LabelDetected = pyResult.LabelDetected;
            result.LabelConfidence = pyResult.LabelConfidence;
            result.LabelCount = pyResult.LabelCount;
            result.LabelTexts = pyResult.LabelTexts ?? new();
            result.LabelKeywordFound = pyResult.LabelKeywordFound;

            result.Message = pyResult.Message;

            _logger.LogInformation(
                "ROI VALIDATED | CameraId={CameraId} ObjDetected={ObjDetected} ObjConf={ObjConf} LabelDetected={LabelDetected} LabelConf={LabelConf}",
                cameraId,
                result.ObjectDetected,
                result.ObjectConfidence,
                result.LabelDetected,
                result.LabelConfidence);

            return result;
        }
    }
}