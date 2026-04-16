using FactoryApi.Contracts.Requests.Camera;
using FactoryApi.Contracts.Responses.Camera;
using FactoryApi.Infrastructure.CameraRuntime;
using FactoryApi.Infrastructure.Persistence;
using FactoryApi.Models;
using Microsoft.EntityFrameworkCore;
using RealtimeEventApi.Application.Camera.Dtos;

namespace FactoryApi.Application.Camera
{
    //나중에 나누기 staus 런타임 제어와 add 설정 관리
    public class CameraCommandService
    {
        private readonly FactoryDbContext _context;
        private readonly CameraOrchestrator _orchestrator;

        public CameraCommandService(FactoryDbContext context, CameraOrchestrator orchestrator)
        {
            _context = context;
            _orchestrator = orchestrator;
        }

        public async Task<CameraRunStatusResponse?> StartCameraAsync(int cameraId, CancellationToken token)
        {

            var cam = await _context.CameraConfigs
              .FirstOrDefaultAsync(x => x.CameraId == cameraId, token);

            if (cam == null)
                return null;

            if (!cam.Enabled)
            {
                cam.Enabled = true;
                await _context.SaveChangesAsync(token);
            }

            bool started = await _orchestrator.StartCameraAsync(cameraId, token);

            if (!started)
            {
                cam.Enabled = false;
                await _context.SaveChangesAsync(token);
            }

            var payload = new CameraRunStatusResponse
            {
                CameraId = cam.CameraId,
                CameraName = cam.CameraName,
                Enabled = cam.Enabled,
                Status = started ? "Running" : "Error",
                Message = started ? "카메라가 즉시 시작되었습니다." : "카메라 시작 실패",
                ChangedAt = DateTime.Now
            };

            return payload;
        }

        public  async Task<CameraRunStatusResponse?> StopCameraAsync(int cameraId, CancellationToken token)
        {
            var cam = await _context.CameraConfigs
               .FirstOrDefaultAsync(x => x.CameraId == cameraId, token);

            if (cam == null)
                return null;

            if (cam.Enabled)
            {
                cam.Enabled = false;
                await _context.SaveChangesAsync(token);
            }

            bool stopped = await _orchestrator.StopCameraAsync(cameraId, token);

            if (!stopped)
            {
                cam.Enabled = true;
                await _context.SaveChangesAsync(token);
            }

            var payload = new CameraRunStatusResponse
            {
                CameraId = cam.CameraId,
                CameraName = cam.CameraName,
                Enabled = cam.Enabled,
                Status = stopped ? "Stopped" : "Error",
                Message = stopped ? "카메라가 즉시 중지되었습니다." : "카메라 중지 실패",
                ChangedAt = DateTime.Now
            };

            return payload;
        }

        public async Task<CameraRunStatusResponse?> GetRunStatusAsync(int cameraId, CancellationToken token)
        {
            var cam = await _context.CameraConfigs
                .FirstOrDefaultAsync(x => x.CameraId == cameraId, token);

            if (cam == null)
                return null;

            bool isRunning = _orchestrator.IsRunning(cameraId);

            string message;
            if (cam.Enabled && isRunning)
                message = "현재 실행 중입니다.";
            else if (!cam.Enabled && !isRunning)
                message = "현재 중지 상태입니다.";
            else if (cam.Enabled && !isRunning)
                message = "실행 요청 상태이지만 아직 세션이 준비되지 않았습니다.";
            else
                message = "중지 요청 상태이지만 세션 정리 중일 수 있습니다.";

            var payload = new CameraRunStatusResponse
            {
                CameraId = cam.CameraId,
                CameraName = cam.CameraName,
                Enabled = cam.Enabled,
                Status = isRunning ? "Running" : "Stopped",
                Message = message,
                ChangedAt = DateTime.Now
            };

            return payload;
        }
        public async Task<AddCameraResult> AddCameraAsync(AddCameraRequest request)
        {
            if (request == null)
            {
                return new AddCameraResult { Success = false};
            }

            var camera = new CameraConfig();
            camera.CameraName = request.CameraName;
            camera.RtspUrl= request.RtspUrl;
            camera.Enabled = request.Enabled;
            camera.ProductName = request.ProductName;
            camera.CreatedAt = DateTime.Now;

            _context.CameraConfigs.Add(camera);
            await _context.SaveChangesAsync();

            return new AddCameraResult
            { Success = true ,
               CameraId = camera.CameraId
            };
        }

        public async Task<DeleteCameraResult> DeleteCameraAsync(int cameraId, CancellationToken token)
        {
            var camera = await _context.CameraConfigs
                .FirstOrDefaultAsync(x => x.CameraId == cameraId, token);

            if (camera == null)
            {
                return new DeleteCameraResult
                {
                    Success = false
                };
            }

            _context.CameraConfigs.Remove(camera);
            await _context.SaveChangesAsync(token);

            return new DeleteCameraResult
            {
                Success = true,
                CameraId = cameraId
            };
        }

    }
}
