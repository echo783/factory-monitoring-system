using RealtimeEventApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using RealtimeEventApi.Contracts.Responses.Camera;

namespace RealtimeEventApi.Infrastructure.CameraRuntime
{
    // TODO: Runtime / Reader / Controller 책임을 분리해 오케스트레이션, 조회, 명령 처리 경계를 명확히 해야 한다.
    public sealed class CameraOrchestrator : BackgroundService, ICameraRuntimeReader, ICameraRuntimeController
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<CameraOrchestrator> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly SnapshotFileService _snapshotFileService;
        private readonly ProductionPersistenceService _persistenceService;
        private readonly ILabelDetector _labelDetector;
        private readonly ICameraStatusPublisher _statusPublisher;
        private readonly CameraRuntimeStatusFactory _statusFactory;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<int, CameraSessionRunner> _sessions = new();
        private readonly System.Collections.Concurrent.ConcurrentDictionary<int, string> _cameraNames = new();
        private readonly System.Collections.Concurrent.ConcurrentDictionary<int, string> _lastStatusSignatures = new();
        private readonly System.Collections.Concurrent.ConcurrentDictionary<int, SemaphoreSlim> _cameraLocks = new();
        // 전체 종료(StopAsync) 시 세션 목록 정리를 보호하는 전역 lock이다.
        // 일반 Start/Stop 및 자동 동기화는 cameraId 단위 lock(_cameraLocks)을 사용한다.
        private readonly SemaphoreSlim _sessionLock = new(1, 1);
        private static readonly TimeSpan ManualStartFrameTimeout = TimeSpan.FromSeconds(12);


        public CameraOrchestrator(
            IServiceScopeFactory scopeFactory,
            ILogger<CameraOrchestrator> logger,
            ILoggerFactory loggerFactory,
            SnapshotFileService snapshotFileService,
            ProductionPersistenceService persistenceService,
            ILabelDetector labelDetector,
            ICameraStatusPublisher statusPublisher,
            CameraRuntimeStatusFactory statusFactory)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _loggerFactory = loggerFactory;
            _snapshotFileService = snapshotFileService;
            _persistenceService = persistenceService;
            _labelDetector = labelDetector;
            _statusPublisher = statusPublisher;
            _statusFactory = statusFactory;
        }

        public CameraSessionSnapshot? GetDebugState(int cameraId)
        {
            if (_sessions.TryGetValue(cameraId, out var session))
                return session.GetDebugState();

            return null;
        }

        public bool IsRunning(int cameraId)
        {
            return _sessions.ContainsKey(cameraId);
        }

        public bool RequestAnalysisReset(int cameraId)
        {
            if (!_sessions.TryGetValue(cameraId, out var session))
                return false;

            session.ResetAnalysisState();
            return true;
        }

        public async Task<CameraRuntimeCommandResult> StartCameraAsync(int cameraId, CancellationToken token = default)
        {
            var camLock = _cameraLocks.GetOrAdd(cameraId, _ => new SemaphoreSlim(1, 1));
            await camLock.WaitAsync(token);
            try
            {
                if (_sessions.TryGetValue(cameraId, out var existingRunner))
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

                var cam = await db.CameraConfigs
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

                _cameraNames[cam.CameraId] = cam.CameraName;

                var runner = new CameraSessionRunner(
                    _loggerFactory.CreateLogger<CameraSessionRunner>(),
                    _snapshotFileService,
                    _persistenceService,
                    cam.CameraId,
                    cam.CameraName,
                    cam.RtspUrl,
                    _labelDetector);

                if (!_sessions.TryAdd(cam.CameraId, runner))
                {
                    await runner.DisposeAsync();
                    return CameraRuntimeCommandResult.Fail("카메라 세션이 이미 생성되었습니다.");
                }

                await runner.StartAsync(token);

                // 즉시 상태 전파 (Starting/Connecting 상태 반영)
                await NotifyStatusAsync(cam.CameraId, cam.CameraName, true, token);

                var connected = await WaitForFirstFrameAsync(runner, ManualStartFrameTimeout, token);

                // 최종 상태 전파 (Running 또는 오류 상태 반영)
                await NotifyStatusAsync(cam.CameraId, cam.CameraName, true, token);

                if (connected)
                    return CameraRuntimeCommandResult.Ok();

                var failedState = runner.GetDebugState();
                var errorMessage = string.IsNullOrWhiteSpace(failedState.LastErrorMessage)
                    ? "카메라 시작 실패: 첫 프레임을 제한 시간 안에 수신하지 못했습니다."
                    : failedState.LastErrorMessage;
                var lastErrorAt = ToNullableTime(failedState.LastErrorAt);

                await runner.StopAsync();
                await runner.DisposeAsync();
                _sessions.TryRemove(cam.CameraId, out _);
                _cameraNames.TryRemove(cam.CameraId, out _);

                _logger.LogWarning(
                    "Manual camera start failed before first frame. CameraId={CameraId}, CameraName={CameraName}",
                    cam.CameraId,
                    cam.CameraName);

                return CameraRuntimeCommandResult.Fail(errorMessage, lastErrorAt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "StartCameraAsync error. CameraId={CameraId}", cameraId);
                return CameraRuntimeCommandResult.Fail($"카메라 시작 중 예외 발생: {ex.Message}");
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

        public async Task<bool> StopCameraAsync(int cameraId, CancellationToken token = default)
        {
            var camLock = _cameraLocks.GetOrAdd(cameraId, _ => new SemaphoreSlim(1, 1));
            await camLock.WaitAsync(token);
            try
            {
                if (!_sessions.TryGetValue(cameraId, out var runner))
                    return true;

                _cameraNames.TryGetValue(cameraId, out var cameraName);

                await runner.StopAsync();
                await runner.DisposeAsync();
                _sessions.TryRemove(cameraId, out _);

                // 즉시 상태 전파 (Stopped 상태 반영)
                await NotifyStatusAsync(cameraId, cameraName ?? string.Empty, false, token);

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

        public int GetCameraCount()
        {
            return _sessions.Count;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("CameraOrchestrator started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var cameraConfigs = await LoadEnabledCamerasAsync(stoppingToken);
                    var currentIds = cameraConfigs.Select(x => x.CameraId).ToHashSet();

                    foreach (var cam in cameraConfigs)
                    {
                        _cameraNames[cam.CameraId] = cam.CameraName;

                        var camLock = _cameraLocks.GetOrAdd(cam.CameraId, _ => new SemaphoreSlim(1, 1));
                        await camLock.WaitAsync(stoppingToken);
                        try
                        {
                            // TODO: ContainsKey 사전 확인 대신 TryAdd 기반의 단일 경로로 통합한다.
                            if (_sessions.ContainsKey(cam.CameraId))
                                continue;

                            var runner = new CameraSessionRunner(
                                _loggerFactory.CreateLogger<CameraSessionRunner>(),
                                _snapshotFileService,
                                _persistenceService,
                                cam.CameraId,
                                cam.CameraName,
                                cam.RtspUrl,
                                _labelDetector);

                            if (_sessions.TryAdd(cam.CameraId, runner))
                            {
                                await runner.StartAsync(stoppingToken);

                                _logger.LogDebug(
                                    "Camera session added. CameraId={CameraId}, CameraName={CameraName}, RtspUrl={RtspUrl}",
                                    cam.CameraId,
                                    cam.CameraName,
                                    cam.RtspUrl);
                            }
                            else
                            {
                                await runner.DisposeAsync();
                            }
                        }
                        finally
                        {
                            camLock.Release();
                        }
                    }

                    var removedIds = _sessions.Keys
                        .Where(id => !currentIds.Contains(id))
                        .ToList();

                    foreach (var id in removedIds)
                    {
                        var camLock = _cameraLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
                        await camLock.WaitAsync(stoppingToken);
                        try
                        {
                            if (_sessions.TryGetValue(id, out var runner))
                            {
                                _cameraNames.TryGetValue(id, out var cameraName);

                                await runner.StopAsync();
                                await runner.DisposeAsync();
                                _sessions.TryRemove(id, out _);
                                _cameraNames.TryRemove(id, out _);

                                _logger.LogInformation(
                                    "Camera session removed. CameraId={CameraId}",
                                    id);

                                await NotifyStatusAsync(id, cameraName ?? string.Empty, false, stoppingToken);
                            }
                        }
                        finally
                        {
                            camLock.Release();
                        }
                    }

                    await PublishCurrentStatusesAsync(cameraConfigs, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "CameraOrchestrator loop error.");
                }

                await Task.Delay(5000, stoppingToken);
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await _sessionLock.WaitAsync(cancellationToken);
            try
            {
                foreach (var pair in _sessions.ToList())
                {
                    try
                    {
                        await pair.Value.StopAsync();
                        await pair.Value.DisposeAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error while stopping session. CameraId={CameraId}", pair.Key);
                    }
                }

                _sessions.Clear();
                _cameraNames.Clear();
                _lastStatusSignatures.Clear();
            }
            finally
            {
                _sessionLock.Release();
            }

            await base.StopAsync(cancellationToken);
        }

        private async Task<List<CameraRuntimeConfig>> LoadEnabledCamerasAsync(CancellationToken token)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FactoryDbContext>();

            return await db.CameraConfigs
                .AsNoTracking()
                .Where(x => x.Enabled)
                .OrderBy(x => x.CameraId)
                .Select(x => new CameraRuntimeConfig
                {
                    CameraId = x.CameraId,
                    CameraName = x.CameraName,
                    RtspUrl = x.RtspUrl
                })
                .ToListAsync(token);
        }

        private async Task PublishCurrentStatusesAsync(
            IReadOnlyCollection<CameraRuntimeConfig> cameraConfigs,
            CancellationToken token)
        {
            foreach (var cam in cameraConfigs)
            {
                await NotifyStatusAsync(cam.CameraId, cam.CameraName, true, token);
            }
        }

        private async Task NotifyStatusAsync(int cameraId, string cameraName, bool enabled, CancellationToken token)
        {
            var sessionExists = _sessions.ContainsKey(cameraId);
            var state = GetDebugState(cameraId);
            var status = _statusFactory.Create(
                cameraId,
                cameraName,
                enabled,
                sessionExists,
                state);

            await PublishIfChangedAsync(status, token);
        }

        private async Task PublishIfChangedAsync(CameraRunStatusResponse status, CancellationToken token)
        {
            var signature = CameraRuntimeStatusFactory.BuildSignature(status);
            if (_lastStatusSignatures.TryGetValue(status.CameraId, out var previous) &&
                previous == signature)
            {
                return;
            }

            _lastStatusSignatures[status.CameraId] = signature;
            await _statusPublisher.PublishAsync(status, token);
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
