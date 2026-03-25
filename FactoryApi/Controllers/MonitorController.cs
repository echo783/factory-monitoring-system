using FactoryApi.Models;
using FactoryApi.Services.CameraRuntime;
using Microsoft.AspNetCore.Mvc;

namespace FactoryApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public sealed class MonitorController : ControllerBase
    {
        private readonly CameraOrchestrator _cameraOrchestrator;

        public MonitorController(CameraOrchestrator cameraOrchestrator)
        {
            _cameraOrchestrator = cameraOrchestrator;
        }

        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            var dto = new
            {
                cameraCount = _cameraOrchestrator.GetCameraCount(),
                workerStatus = "running",
                serverTime = DateTime.Now
            };

            return Ok(dto);
        }

        [HttpGet("debug/{cameraId:int}")]
        public ActionResult<DebugDto> GetDebug(int cameraId)
        {
            var state = _cameraOrchestrator.GetDebugState(cameraId);
            if (state == null)
            {
                return NotFound(new
                {
                    message = $"Camera session not found. cameraId={cameraId}"
                });
            }

            var dto = new DebugDto
            {
                RotationActive = state.RotationActive,
                LabelInZone = state.LabelInZone,

                LastStarted = state.LastStarted,
                LastEnded = state.LastEnded,
                LastLabelEnter = state.LastLabelEnter,

                LastRotationChangeValue = state.LastRotationChangeValue,
                LastMotionRatio = state.LastMotionRatio,
                LastLabelChangeValue = state.LastLabelChangeValue,

                ProductionCount = state.ProductionCount,

                LastProductionAt = state.LastProductionAt == DateTime.MinValue
                    ? (DateTime?)null
                    : state.LastProductionAt
            };

            return Ok(dto);
        }

        [HttpGet("production/{cameraId:int}")]
        public IActionResult GetProduction(int cameraId)
        {
            var state = _cameraOrchestrator.GetDebugState(cameraId);
            if (state == null)
            {
                return NotFound(new
                {
                    message = $"Camera not found. id={cameraId}"
                });
            }

            var dto = new
            {
                cameraId = cameraId,
                productionCount = state.ProductionCount,
                lastProductionAt = state.LastProductionAt == DateTime.MinValue
                    ? (DateTime?)null
                    : state.LastProductionAt
            };

            return Ok(dto);
        }
    }
}