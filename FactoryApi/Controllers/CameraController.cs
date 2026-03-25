using FactoryApi.Data;
using FactoryApi.Models;
using FactoryApi.Services.CameraRuntime;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FactoryApi.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace FactoryApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CameraController : ControllerBase
    {
        private readonly FactoryDbContext _context;
        private readonly ILogger<CameraController> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly CameraOrchestrator _orchestrator;
        private readonly IHubContext<CameraHub> _hubContext;

        public CameraController(
    FactoryDbContext context,
    ILogger<CameraController> logger,
    IWebHostEnvironment env,
    CameraOrchestrator orchestrator,
    IHubContext<CameraHub> hubContext)
        {
            _context = context;
            _logger = logger;
            _env = env;
            _orchestrator = orchestrator;
            _hubContext = hubContext;
        }

        // 1. 전체 카메라 목록 조회
        [HttpGet("list")]
        public async Task<IActionResult> GetList()
        {
            var list = await _context.CameraConfigs
                .OrderBy(x => x.CameraId)
                .ToListAsync();

            return Ok(list);
        }

        // 2. 활성 카메라 목록 조회
        [HttpGet("enabled")]
        public async Task<IActionResult> GetEnabledList()
        {
            var list = await _context.CameraConfigs
                .Where(x => x.Enabled)
                .OrderBy(x => x.CameraId)
                .ToListAsync();

            return Ok(list);
        }

        // 3. 카메라 1건 조회
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var camera = await _context.CameraConfigs
                .FirstOrDefaultAsync(x => x.CameraId == id);

            if (camera == null)
                return NotFound($"CameraId={id} 카메라를 찾을 수 없습니다.");

            return Ok(camera);
        }

        // 4. 카메라 추가
        [HttpPost("add")]
        public async Task<IActionResult> Add([FromBody] CameraConfig camera)
        {
            if (camera == null)
                return BadRequest("카메라 정보가 없습니다.");

            camera.CreatedAt = DateTime.Now;

            _context.CameraConfigs.Add(camera);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "카메라 저장 완료",
                cameraId = camera.CameraId
            });
        }

        // 5. 최신 이미지 반환
        [HttpGet("{cameraId:int}/image")]
        public async Task<IActionResult> GetImage(int cameraId, CancellationToken ct)
        {
            try
            {
                bool cameraExists = await _context.CameraConfigs
                    .AnyAsync(x => x.CameraId == cameraId, ct);

                if (!cameraExists)
                    return NotFound($"CameraId={cameraId} 카메라를 찾을 수 없습니다.");

                string imagePath = Path.Combine(
                    _env.ContentRootPath,
                    "captures",
                    $"cam{cameraId}",
                    "latest.jpg");

                if (!System.IO.File.Exists(imagePath))
                    return NotFound("latest 이미지가 없습니다.");

                byte[] bytes = await ReadFileSafeAsync(imagePath, ct);
                return File(bytes, "image/jpeg");
            }
            catch (OperationCanceledException)
            {
                // 브라우저가 이전 이미지 요청을 취소한 경우
                return new EmptyResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetImage error. CameraId={CameraId}", cameraId);
                return StatusCode(500, "이미지 읽기 실패");
            }
        }

        // 6. ROI 디버그용 설정 조회
        [HttpGet("{cameraId:int}/debug-config")]
        public async Task<IActionResult> GetDebugConfig(int cameraId, CancellationToken token)
        {
            var cam = await _context.CameraConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.CameraId == cameraId, token);

            if (cam == null)
                return NotFound($"CameraId={cameraId} 카메라를 찾을 수 없습니다.");

            return Ok(new
            {
                cam.CameraId,
                cam.CameraName,
                cam.ProductName,
                cam.CheckRotation,
                cam.CheckLabel,
                cam.ObjectRoiX,
                cam.ObjectRoiY,
                cam.ObjectRoiW,
                cam.ObjectRoiH,
                cam.LabelRoiX,
                cam.LabelRoiY,
                cam.LabelRoiW,
                cam.LabelRoiH,
                latestUrl = $"/api/Camera/{cameraId}/image?t={DateTimeOffset.Now.ToUnixTimeMilliseconds()}"
            });
        }

        // 7. 실시간 디버그 상태 조회
        [HttpGet("{cameraId:int}/debug-state")]
        public IActionResult GetDebugState(int cameraId)
        {
            var session = _orchestrator.GetSession(cameraId);
            if (session == null)
                return NotFound($"CameraId={cameraId} 실행 세션을 찾을 수 없습니다.");

            var s = session.GetDebugState();

            return Ok(new
            {
                s.RotationActive,
                s.LabelInZone,
                s.CountedInCurrentRotation,
                s.LastRotationChangeValue,
                s.LastMotionRatio,
                s.LastLabelChangeValue,
                s.LastStarted,
                s.LastEnded,
                s.LastLabelEnter,
                s.LastDetectorFound,
                s.LastDetectorConfidence,
                s.LabelDetectedStreak,
                s.LabelOffStreak,
                s.ProductionCount,
                s.LastFrameAt,
                s.LastSuccessfulReadAt,
                s.LastReconnectAt,
                s.SessionStartedAt,
                s.LastUpdatedAt,
                s.StreamJustReconnected
            });
        }

        // 8. ROI 저장
        [HttpPost("{cameraId:int}/roi")]
        public async Task<IActionResult> SaveRoi(
            int cameraId,
            [FromBody] SaveRoiRequest request,
            CancellationToken token)
        {
            var cam = await _context.CameraConfigs
                .FirstOrDefaultAsync(x => x.CameraId == cameraId, token);

            if (cam == null)
                return NotFound($"CameraId={cameraId} 카메라를 찾을 수 없습니다.");

            if (request.ObjectRoiW <= 0 || request.ObjectRoiH <= 0 ||
                request.LabelRoiW <= 0 || request.LabelRoiH <= 0)
            {
                _logger.LogWarning(
                    "ROI INVALID INPUT | CameraId={CameraId} Obj=({OX},{OY},{OW},{OH}) Label=({LX},{LY},{LW},{LH})",
                    cameraId,
                    request.ObjectRoiX, request.ObjectRoiY, request.ObjectRoiW, request.ObjectRoiH,
                    request.LabelRoiX, request.LabelRoiY, request.LabelRoiW, request.LabelRoiH);

                return BadRequest("ROI 폭/높이는 0보다 커야 합니다.");
            }

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

            await _context.SaveChangesAsync(token);

            // ✅ ROI 크기/위치가 바뀌면 이전 프레임과 현재 프레임 크기가 달라질 수 있으므로
            //    현재 세션의 분석 상태를 초기화해서 OpenCV 예외를 방지
            var session = _orchestrator.GetSession(cameraId);
            session?.ResetAnalysisState();

            _logger.LogInformation(
                "ROI SAVED | CameraId={CameraId} Obj=({OX},{OY},{OW},{OH}) Label=({LX},{LY},{LW},{LH})",
                cameraId,
                cam.ObjectRoiX, cam.ObjectRoiY, cam.ObjectRoiW, cam.ObjectRoiH,
                cam.LabelRoiX, cam.LabelRoiY, cam.LabelRoiW, cam.LabelRoiH);

            return Ok(new
            {
                ok = true,
                message = "ROI 저장 완료"
            });
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


        // 9. 카메라 시작
        [HttpPost("{cameraId:int}/start")]
        public async Task<IActionResult> StartCamera(int cameraId, CancellationToken token)
        {
            var cam = await _context.CameraConfigs
                .FirstOrDefaultAsync(x => x.CameraId == cameraId, token);

            if (cam == null)
                return NotFound($"CameraId={cameraId} 카메라를 찾을 수 없습니다.");

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

            var payload = new CameraControlStatusDto
            {
                CameraId = cam.CameraId,
                CameraName = cam.CameraName,
                Enabled = cam.Enabled,
                Status = started ? "Running" : "Error",
                Message = started ? "카메라가 즉시 시작되었습니다." : "카메라 시작 실패",
                ChangedAt = DateTime.Now
            };

            await _hubContext.Clients.All.SendAsync("CameraStatusChanged", payload, token);

            return Ok(payload);
        }

        // 10. 카메라 중지
        [HttpPost("{cameraId:int}/stop")]
        public async Task<IActionResult> StopCamera(int cameraId, CancellationToken token)
        {
            var cam = await _context.CameraConfigs
                .FirstOrDefaultAsync(x => x.CameraId == cameraId, token);

            if (cam == null)
                return NotFound($"CameraId={cameraId} 카메라를 찾을 수 없습니다.");

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

            var payload = new CameraControlStatusDto
            {
                CameraId = cam.CameraId,
                CameraName = cam.CameraName,
                Enabled = cam.Enabled,
                Status = stopped ? "Stopped" : "Error",
                Message = stopped ? "카메라가 즉시 중지되었습니다." : "카메라 중지 실패",
                ChangedAt = DateTime.Now
            };

            await _hubContext.Clients.All.SendAsync("CameraStatusChanged", payload, token);

            return Ok(payload);
        }

        // 11. 카메라 상태 조회
        [HttpGet("{cameraId:int}/status")]
        public async Task<IActionResult> GetRunStatus(int cameraId, CancellationToken token)
        {
            var cam = await _context.CameraConfigs
                .FirstOrDefaultAsync(x => x.CameraId == cameraId, token);

            if (cam == null)
                return NotFound($"CameraId={cameraId} 카메라를 찾을 수 없습니다.");

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

            var payload = new CameraControlStatusDto
            {
                CameraId = cam.CameraId,
                CameraName = cam.CameraName,
                Enabled = cam.Enabled,
                Status = isRunning ? "Running" : "Stopped",
                Message = message,
                ChangedAt = DateTime.Now
            };

            return Ok(payload);
        }

    }

    public sealed class SaveRoiRequest
    {
        public int ObjectRoiX { get; set; }
        public int ObjectRoiY { get; set; }
        public int ObjectRoiW { get; set; }
        public int ObjectRoiH { get; set; }

        public int LabelRoiX { get; set; }
        public int LabelRoiY { get; set; }
        public int LabelRoiW { get; set; }
        public int LabelRoiH { get; set; }

        public bool CheckRotation { get; set; }
        public bool CheckLabel { get; set; }
    }
}