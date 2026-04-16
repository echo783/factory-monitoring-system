using FactoryApi.Contracts.Requests.Camera;
using FactoryApi.Infrastructure.CameraRuntime;
using FactoryApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using RealtimeEventApi.Application.Camera.Dtos;

namespace RealtimeEventApi.Application.Camera
{
    public class CameraRoiService
    {
        private readonly FactoryDbContext _context;
        private readonly ILogger<CameraRoiService> _logger;
        private readonly CameraOrchestrator _orchestrator;

        public CameraRoiService(
            FactoryDbContext context,
            ILogger<CameraRoiService> logger,
            CameraOrchestrator cameraOrchestrator)
        {
            _context = context;
            _logger = logger;
            _orchestrator = cameraOrchestrator;
        }

        public async Task<CameraRoiResult> SaveRoiAsync(
            int cameraId,
            SaveRoiRequest request,
            CancellationToken ct)
        {
            var result = new CameraRoiResult();

            var cam = await _context.CameraConfigs
                .FirstOrDefaultAsync(x => x.CameraId == cameraId, ct);

            if (cam == null)
            {
                result.CameraExists = false;
                result.IsValidInput = false;
                return result;
            }

            result.CameraExists = true;

            cam.ObjectRoiX = request.ObjectRoiX;
            cam.ObjectRoiY = request.ObjectRoiY;
            cam.ObjectRoiW = request.ObjectRoiW;
            cam.ObjectRoiH = request.ObjectRoiH;

            cam.LabelRoiX = request.LabelRoiX;
            cam.LabelRoiY = request.LabelRoiY;
            cam.LabelRoiW = request.LabelRoiW;
            cam.LabelRoiH = request.LabelRoiH;

            cam.CheckRotation = request.CheckRotation;
            cam.CheckLabel = request.CheckLabel;

            await _context.SaveChangesAsync(ct);

            var session = _orchestrator.GetSession(cameraId);
            session?.ResetAnalysisState();

            _logger.LogDebug(
                "ROI SAVED | CameraId={CameraId} Obj=({OX},{OY},{OW},{OH}) Label=({LX},{LY},{LW},{LH})",
                cameraId,
                cam.ObjectRoiX, cam.ObjectRoiY, cam.ObjectRoiW, cam.ObjectRoiH,
                cam.LabelRoiX, cam.LabelRoiY, cam.LabelRoiW, cam.LabelRoiH);

            result.IsValidInput = true;
            return result;
        }
         
    }
}