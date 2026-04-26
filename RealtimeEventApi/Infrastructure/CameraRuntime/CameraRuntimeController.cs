using RealtimeEventApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace RealtimeEventApi.Infrastructure.CameraRuntime
{
    public sealed class CameraRuntimeController : ICameraRuntimeController
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly CameraRuntimeRegistry _registry;
        private readonly CameraRuntimeLifecycleState _lifecycleState;
        private readonly CameraSessionRunnerFactory _runnerFactory;
        private readonly CameraRuntimeSessionLifecycle _sessionLifecycle;
        private readonly ILogger<CameraRuntimeController> _logger;
        private static readonly TimeSpan ManualStartFrameTimeout = TimeSpan.FromSeconds(12);
        private readonly CameraRuntimeStatusNotifier _statusNotifier;

        public CameraRuntimeController(
            IServiceScopeFactory scopeFactory,
            CameraRuntimeRegistry registry,
            CameraRuntimeLifecycleState lifecycleState,
            CameraSessionRunnerFactory runnerFactory,
            CameraRuntimeSessionLifecycle sessionLifecycle,
            ILogger<CameraRuntimeController> logger,
            CameraRuntimeStatusNotifier statusNotifier)
        {
            _scopeFactory = scopeFactory;
            _registry = registry;
            _lifecycleState = lifecycleState;
            _runnerFactory = runnerFactory;
            _sessionLifecycle = sessionLifecycle;
            _logger = logger;
            _statusNotifier = statusNotifier;
        }

        public bool IsRunning(int cameraId)
        {
            return _registry.HasRunner(cameraId);
        }

        public bool RequestAnalysisReset(int cameraId)
        {
            var runner = _registry.GetRunner(cameraId);
            if (runner == null)
                return false;

            runner.ResetAnalysisState();
            return true;
        }

        public async Task<CameraRuntimeCommandResult> StartCameraAsync(int cameraId, CancellationToken token = default)
        {
            if (_lifecycleState.IsShuttingDown)
                return CameraRuntimeCommandResult.Fail("애플리케이션 종료 중에는 카메라를 시작할 수 없습니다.");

            var entry = _registry.GetEntry(cameraId);
            var camLock = entry.Lock;
            await camLock.WaitAsync(token);
            CameraRuntimeConfig? cam = null;
            CameraSessionRunner? runner = null;
            var sessionAdded = false;
            try
            {
                if (_lifecycleState.IsShuttingDown)
                    return CameraRuntimeCommandResult.Fail("애플리케이션 종료 중에는 카메라를 시작할 수 없습니다.");

                var existingRunner = _registry.GetRunner(cameraId);
                if (existingRunner != null)
                {
                    var existingState = existingRunner.GetDebugState();
                    return HasSuccessfulFrame(existingState)
                        ? CameraRuntimeCommandResult.Ok()
                        : CameraRuntimeCommandResult.Fail(
                            string.IsNullOrWhiteSpace(existingState.LastErrorMessage)
                                ? "세션은 존재하지만 아직 카메라 프레임을 수신하지 못했습니다."
                                : existingState.LastErrorMessage,
                            ToNullableTime(existingState.LastErrorAt));
                }

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<FactoryDbContext>();

                cam = await db.CameraConfigs
                    .AsNoTracking()
                    .Where(x => x.CameraId == cameraId)
                    .Select(x => new CameraRuntimeConfig
                    {
                        CameraId = x.CameraId,
                        CameraName = x.CameraName,
                        RtspUrl = x.RtspUrl
                    })
                    .FirstOrDefaultAsync(token);

                if (cam == null)
                    return CameraRuntimeCommandResult.Fail($"CameraId={cameraId} 카메라를 찾을 수 없습니다.");

                entry.CameraName = cam.CameraName;

                runner = _runnerFactory.Create(cam.CameraId, cam.CameraName, cam.RtspUrl);

                if (!_registry.TrySetRunner(cam.CameraId, runner))
                {
                    await _sessionLifecycle.StopAndDisposeRunnerAsync(cam.CameraId, runner, "manual start duplicate session cleanup");
                    return CameraRuntimeCommandResult.Fail("카메라 세션이 이미 생성되었습니다.");
                }

                sessionAdded = true;
                await runner.StartAsync(token);

                if (_lifecycleState.IsShuttingDown)
                {
                    await RemoveStopAndDisposeSessionAsync(cam.CameraId, runner, "manual start interrupted by shutdown");
                    return CameraRuntimeCommandResult.Fail("애플리케이션 종료 중에는 카메라를 시작할 수 없습니다.");
                }

                // 즉시 상태 전파 (Starting/Connecting 상태 반영)
                await _statusNotifier.NotifyStatusAsync(cam.CameraId, cam.CameraName, true, token);

                var connected = await WaitForFirstFrameAsync(runner, ManualStartFrameTimeout, token);

                if (_lifecycleState.IsShuttingDown)
                {
                    await RemoveStopAndDisposeSessionAsync(cam.CameraId, runner, "manual start completed during shutdown");
                    return CameraRuntimeCommandResult.Fail("애플리케이션 종료 중에는 카메라를 시작할 수 없습니다.");
                }

                // 최종 상태 전파 (Running 또는 오류 상태 반영)
                await _statusNotifier.NotifyStatusAsync(cam.CameraId, cam.CameraName, true, token);

                if (connected)
                    return CameraRuntimeCommandResult.Ok();

                var failedState = runner.GetDebugState();
                var errorMessage = string.IsNullOrWhiteSpace(failedState.LastErrorMessage)
                    ? "카메라 시작 실패: 첫 프레임을 제한 시간 안에 수신하지 못했습니다."
                    : failedState.LastErrorMessage;
                var lastErrorAt = ToNullableTime(failedState.LastErrorAt);

                await RemoveStopAndDisposeSessionAsync(cam.CameraId, runner, "manual start first frame timeout");

                _logger.LogWarning(
                    "Manual camera start failed before first frame. CameraId={CameraId}, CameraName={CameraName}",
                    cam.CameraId,
                    cam.CameraName);

                return CameraRuntimeCommandResult.Fail(errorMessage, lastErrorAt);
            }
            catch (Exception ex)
            {
                if (runner != null)
                {
                    var cleanupCameraId = cam?.CameraId ?? cameraId;
                    if (sessionAdded)
                        await RemoveStopAndDisposeSessionAsync(cleanupCameraId, runner, "manual start exception cleanup");
                    else
                        await _sessionLifecycle.StopAndDisposeRunnerAsync(cleanupCameraId, runner, "manual start exception cleanup");
                }

                _logger.LogError(ex, "StartCameraAsync error. CameraId={CameraId}", cameraId);
                return CameraRuntimeCommandResult.Fail($"카메라 시작 중 예외 발생: {ex.Message}");
            }
            finally
            {
                camLock.Release();
            }
        }

        public async Task<bool> StopCameraAsync(int cameraId, CancellationToken token = default)
        {
            var entry = _registry.GetEntry(cameraId);
            var camLock = entry.Lock;
            await camLock.WaitAsync(token);
            try
            {
                if (!_registry.TryTakeRunner(cameraId, out var runner))
                    return true;

                var cameraName = entry.CameraName;

                await _sessionLifecycle.StopAndDisposeRunnerAsync(cameraId, runner!, "manual stop");
                entry.CameraName = string.Empty;
                entry.LastStatusSignature = null;

                // 즉시 상태 전파 (Stopped 상태 반영)
                await _statusNotifier.NotifyStatusAsync(cameraId, cameraName ?? string.Empty, false, token);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "StopCameraAsync error. CameraId={CameraId}", cameraId);
                return false;
            }
            finally
            {
                camLock.Release();
            }
        }

        private static async Task<bool> WaitForFirstFrameAsync(
            CameraSessionRunner runner,
            TimeSpan timeout,
            CancellationToken token)
        {
            var deadline = DateTime.UtcNow + timeout;

            while (DateTime.UtcNow < deadline)
            {
                token.ThrowIfCancellationRequested();

                if (HasSuccessfulFrame(runner.GetDebugState()))
                    return true;

                await Task.Delay(200, token);
            }

            return false;
        }

        private static bool HasSuccessfulFrame(CameraSessionSnapshot state)
        {
            return state.LastSuccessfulReadAt != DateTime.MinValue;
        }

        private async Task RemoveStopAndDisposeSessionAsync(
            int cameraId,
            CameraSessionRunner runner,
            string operation)
        {
            if (_registry.TryTakeRunner(cameraId, out var registeredRunner))
            {
                await _sessionLifecycle.StopAndDisposeRunnerAsync(cameraId, registeredRunner!, operation);
            }
            else
            {
                await _sessionLifecycle.StopAndDisposeRunnerAsync(cameraId, runner, operation);
            }

            _registry.GetEntry(cameraId).CameraName = string.Empty;
            _registry.GetEntry(cameraId).LastStatusSignature = null;
        }

        private static DateTime? ToNullableTime(DateTime value)
        {
            return value == DateTime.MinValue ? null : value;
        }

        private sealed class CameraRuntimeConfig
        {
            public int CameraId { get; set; }
            public string CameraName { get; set; } = string.Empty;
            public string RtspUrl { get; set; } = string.Empty;
        }
    }
}
