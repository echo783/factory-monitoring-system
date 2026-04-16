using FactoryApi.Application.Ai;
using FactoryApi.Application.Camera;
using FactoryApi.Contracts.Requests.Camera;
using FactoryApi.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using RealtimeEventApi.Application.Camera;

namespace FactoryApi.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class CameraController : ControllerBase
    {
        private readonly ILogger<CameraController> _logger;
        private readonly IHubContext<CameraHub> _hubContext;
        private readonly CameraQueryService _queryService;
        private readonly CameraDebugService _debugService;
        private readonly CameraImageService _imageService;
        private readonly CameraRoiService _roiService;
        private readonly CameraCommandService _commandService;
        private readonly CameraRoiValidationService _roiValidationService;

        public CameraController(
        CameraQueryService queryService,
        CameraCommandService commandService,
        CameraRoiService roiService,
        CameraImageService imageService,
        CameraDebugService debugService,
        ILogger<CameraController> logger,
        IHubContext<CameraHub> hubContext,
        CameraRoiValidationService roiValidationService
        )
        {
            _queryService = queryService;
            _debugService = debugService;
            _imageService = imageService;
            _roiService = roiService;
            _commandService = commandService;   
            _logger = logger;
            _hubContext = hubContext;
             _roiValidationService = roiValidationService;
        }

        // 1. 전체 카메라 목록 조회
        [HttpGet("list")]
        public async Task<IActionResult> GetList()
        {
            var result = await _queryService.GetListAsync();

            if (result == null)
                return NotFound($"등록된 카메라가 없습니다.");

            return Ok(result);
        }

        // 2. 활성 카메라 목록 조회
        [HttpGet("enabled")]
        public async Task<IActionResult> GetEnabledList()
        {
            var result = await _queryService.GetEnabledListAsync();

            if (result == null)
                return NotFound($"활성 등록된 카메라가 없습니다.");

            return Ok(result);
        }

        // 3. 카메라 1건 조회
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var camera = await _queryService.GetByIdAsync(id);

            if (camera == null)
                return NotFound($"CameraId={id} 카메라를 찾을 수 없습니다.");

            return Ok(camera);
        }

        // 4. 카메라 추가
        [HttpPost("add")]
        public async Task<IActionResult> Add([FromBody] AddCameraRequest request)
        {
            var result = await _commandService.AddCameraAsync(request);  

            if (!result.Success) 
                return BadRequest("카메라 정보가 없습니다.");

            return  Ok(new
            {
                message = "카메라 저장 완료",
                cameraId = result.CameraId
            });

        }

        // 5. 최신 이미지 반환
        [AllowAnonymous]
        [HttpGet("{cameraId:int}/image")]
        public async Task<IActionResult> GetImage(int cameraId, CancellationToken ct)
        {
            try
            {
                var image = await _imageService.GetImageAsync(cameraId, ct);
                if (!image.CameraExists)
                    return NotFound($"CameraId={cameraId} 카메라를 찾을 수 없습니다.");

                if (!image.ImageExists || image.Bytes == null)
                    return new EmptyResult();

                return File(image.Bytes, "image/jpeg");
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
           var result = await _debugService.GetDebugConfigAsync(cameraId, token);  
            
            if (result == null) return NotFound($"CameraId={cameraId} 카메라를 찾을 수 없습니다.");

            return Ok(result);
        }

        // 7. 실시간 디버그 상태 조회
        [HttpGet("{cameraId:int}/debug-state")]
        public IActionResult GetDebugState(int cameraId)
        {
            var result = _debugService.GetDebugState(cameraId);
            if (result == null)
                return NotFound($"CameraId={cameraId} 실행 세션을 찾을 수 없습니다.");
            return Ok(result);
        }

        // 8. ROI 저장
        [HttpPost("{cameraId:int}/roi")]
        public async Task<IActionResult> SaveRoi(
            int cameraId,
            [FromBody] SaveRoiRequest request,
            CancellationToken token)
        {
            var roi = await _roiService.SaveRoiAsync(cameraId, request, token);

            if (!roi.CameraExists)
                return NotFound($"CameraId={cameraId} 카메라를 찾을 수 없습니다.");

            if (!roi.IsValidInput)
                return BadRequest("ROI 폭/높이는 0보다 커야 합니다.");

            return Ok(new
                {
                    ok = true,
                    message = "ROI 저장 완료"
                });
        }

        // 9. 카메라 시작
        [HttpPost("{cameraId:int}/start")]
        public async Task<IActionResult> StartCamera(int cameraId, CancellationToken token)
        {
            var result = await _commandService.StartCameraAsync(cameraId, token);
            
            if (result == null) return NotFound($"CameraId={cameraId} 카메라를 찾을 수 없습니다.");

            await _hubContext.Clients.Group($"camera-{cameraId}").SendAsync("CameraStatusChanged", result, token);

            return Ok(result);
        }

        // 10. 카메라 중지
        [HttpPost("{cameraId:int}/stop")]
        public async Task<IActionResult> StopCamera(int cameraId, CancellationToken token)
        {
           var result = await _commandService.StopCameraAsync(cameraId, token);

            if (result == null) return NotFound($"CameraID={cameraId} 카메라를 찾을 수 없습니다.");

            await _hubContext.Clients.Group($"camera-{cameraId}").SendAsync("CameraStatusChanged", result, token);

            return Ok(result);
        }

        // 11. 카메라 상태 조회
        [HttpGet("{cameraId:int}/status")]
        public async Task<IActionResult> GetRunStatus(int cameraId, CancellationToken token)
        {
            var result = await _commandService.GetRunStatusAsync(cameraId, token);

            if (result == null) return NotFound($"CameraId={cameraId} 카메라를 찾을 수 없습니다.");

            return Ok(result);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken token)
        {
            var result = await _commandService.DeleteCameraAsync(id, token);

            if (result == null || !result.Success)
                return NotFound($"CameraId={id} 카메라를 찾을 수 없습니다.");

            return Ok(new
            {
                message = "카메라 삭제 완료",
                cameraId = id
            });
        }

        [HttpPost("{cameraId:int}/validate-roi")]
        public async Task<IActionResult> ValidateRoi(
     int cameraId,
     CancellationToken token)
        {
            var result = await _roiValidationService.ValidateAsync(cameraId, token);

            if (!result.CameraExists)
                return NotFound(result.Message);

            if (!result.ImageExists)
                return NotFound(result.Message);

            return Ok(new
            {
                ok = result.Success,
                message = result.Message,
                imagePath = result.ImagePath,

                objectDetected = result.ObjectDetected,
                objectConfidence = result.ObjectConfidence,
                objectCount = result.ObjectCount,
                objectClasses = result.ObjectClasses,

                labelDetected = result.LabelDetected,
                labelConfidence = result.LabelConfidence,
                labelCount = result.LabelCount,
                labelTexts = result.LabelTexts,
                labelKeywordFound = result.LabelKeywordFound
            });
        }

    }
}