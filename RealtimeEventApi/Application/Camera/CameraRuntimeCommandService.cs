using RealtimeEventApi.Contracts.Responses.Camera;
using RealtimeEventApi.Infrastructure.CameraRuntime;
using RealtimeEventApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace RealtimeEventApi.Application.Camera
{
    public class CameraRuntimeCommandService
    {
        private readonly FactoryDbContext _context;
        private readonly ICameraRuntimeController _cameraRuntimeController;
        private readonly ICameraRuntimeReader _cameraRuntimeReader;
        private readonly CameraRuntimeStatusFactory _statusFactory;

        public CameraRuntimeCommandService(
            FactoryDbContext context,
            ICameraRuntimeController cameraRuntimeController,
            ICameraRuntimeReader cameraRuntimeReader,
            CameraRuntimeStatusFactory statusFactory)
        {
            _context = context;
            _cameraRuntimeController = cameraRuntimeController;
            _cameraRuntimeReader = cameraRuntimeReader;
            _statusFactory = statusFactory;
        }

        public async Task<CameraRunStatusResponse?> StartCameraAsync(int cameraId, CancellationToken token)
        {
            var cam = await _context.CameraConfigs
                .FirstOrDefaultAsync(x => x.CameraId == cameraId, token);

            if (cam == null)
                return null;

            // 1. DB에 먼저 활성화 상태 기록 (Orchestrator 루프에서 세션이 제거되지 않도록 보호)
            bool wasAlreadyEnabled = cam.Enabled;
            if (!wasAlreadyEnabled)
            {
                cam.Enabled = true;
                await _context.SaveChangesAsync(token);
            }

            // 2. 실제 카메라 시작 시도 (내부에서 WaitForFirstFrameAsync로 대기)
            var startResult = await _cameraRuntimeController.StartCameraAsync(cameraId, token);

            // 3. 실패 시 Enabled 상태 롤백
            if (!startResult.Success && !wasAlreadyEnabled)
            {
                cam.Enabled = false;
                await _context.SaveChangesAsync(token);
            }

            var state = startResult.Success ? _cameraRuntimeReader.GetDebugState(cameraId) : null;

            return startResult.Success
                ? _statusFactory.Create(
                    cam.CameraId,
                    cam.CameraName,
                    cam.Enabled,
                    _cameraRuntimeController.IsRunning(cameraId),
                    state)
                : new CameraRunStatusResponse
                {
                    CameraId = cam.CameraId,
                    CameraName = cam.CameraName,
                    Enabled = cam.Enabled,
                    Status = "Error",
                    Message = string.IsNullOrWhiteSpace(startResult.ErrorMessage)
                        ? "카메라 시작 실패"
                        : startResult.ErrorMessage,
                    ChangedAt = DateTime.Now,
                    LastSuccessfulReadAt = null,
                    LastErrorAt = startResult.LastErrorAt,
                    LastErrorMessage = startResult.ErrorMessage,
                    ProductionCount = 0
                };
        }

        public async Task<CameraRunStatusResponse?> StopCameraAsync(int cameraId, CancellationToken token)
        {
            var cam = await _context.CameraConfigs
                .FirstOrDefaultAsync(x => x.CameraId == cameraId, token);

            if (cam == null)
                return null;

            bool stopped = await _cameraRuntimeController.StopCameraAsync(cameraId, token);

            if (stopped && cam.Enabled)
            {
                cam.Enabled = false;
                await _context.SaveChangesAsync(token);
            }

            var state = stopped ? null : _cameraRuntimeReader.GetDebugState(cameraId);

            return stopped
                ? _statusFactory.Create(
                    cam.CameraId,
                    cam.CameraName,
                    enabled: false,
                    sessionExists: false,
                    state: null)
                : new CameraRunStatusResponse
                {
                    CameraId = cam.CameraId,
                    CameraName = cam.CameraName,
                    Enabled = cam.Enabled,
                    Status = "Error",
                    Message = "카메라 중지 실패",
                    ChangedAt = DateTime.Now,
                    LastSuccessfulReadAt = null,
                    LastErrorAt = null,
                    LastErrorMessage = state?.LastErrorMessage ?? string.Empty,
                    ProductionCount = state?.ProductionCount ?? 0
                };
        }

        public async Task<CameraRunStatusResponse?> GetRunStatusAsync(int cameraId, CancellationToken token)
        {
            var cam = await _context.CameraConfigs
                .FirstOrDefaultAsync(x => x.CameraId == cameraId, token);

            if (cam == null)
                return null;

            bool isRunning = _cameraRuntimeController.IsRunning(cameraId);
            var state = _cameraRuntimeReader.GetDebugState(cameraId);

            return _statusFactory.Create(
                cam.CameraId,
                cam.CameraName,
                cam.Enabled,
                isRunning,
                state);
        }
    }
}
