using RealtimeEventApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace RealtimeEventApi.Infrastructure.CameraRuntime
{
    // TODO: Runtime / Reader / Controller 책임을 분리해 오케스트레이션, 조회, 명령 처리 경계를 명확히 해야 한다.
    public sealed class CameraOrchestrator : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<CameraOrchestrator> _logger;
        private readonly CameraSessionRunnerFactory _runnerFactory;
        private readonly CameraRuntimeRegistry _registry;
        private readonly CameraRuntimeLifecycleState _lifecycleState;
        private readonly CameraRuntimeSessionLifecycle _sessionLifecycle;
        private readonly CameraRuntimeStatusNotifier _statusNotifier;

        public CameraOrchestrator(
            IServiceScopeFactory scopeFactory,
            ILogger<CameraOrchestrator> logger,
            CameraSessionRunnerFactory runnerFactory,
            CameraRuntimeRegistry registry,
            CameraRuntimeLifecycleState lifecycleState,
            CameraRuntimeSessionLifecycle sessionLifecycle,
            CameraRuntimeStatusNotifier statusNotifier)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _runnerFactory = runnerFactory;
            _registry = registry;
            _lifecycleState = lifecycleState;
            _sessionLifecycle = sessionLifecycle;
            _statusNotifier = statusNotifier;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("CameraOrchestrator started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                if (_lifecycleState.IsShuttingDown)
                {
                    _logger.LogInformation("CameraOrchestrator sync loop stopped because application is shutting down.");
                    break;
                }

                try
                {
                    var cameraConfigs = await LoadEnabledCamerasAsync(stoppingToken);
                    var currentIds = cameraConfigs.Select(x => x.CameraId).ToHashSet();

                    foreach (var cam in cameraConfigs)
                    {
                        var entry = _registry.GetEntry(cam.CameraId);
                        entry.CameraName = cam.CameraName;

                        var camLock = entry.Lock;
                        await camLock.WaitAsync(stoppingToken);
                        try
                        {
                            // TODO: ContainsKey 사전 확인 대신 TryAdd 기반의 단일 경로로 통합한다.
                            if (_registry.HasRunner(cam.CameraId))
                                continue;

                            var runner = _runnerFactory.Create(cam.CameraId, cam.CameraName, cam.RtspUrl);

                            if (_registry.TrySetRunner(cam.CameraId, runner))
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

                    var removedIds = _registry.GetRunningCameraIds()
                        .Where(id => !currentIds.Contains(id))
                        .ToList();

                    foreach (var id in removedIds)
                    {
                        var entry = _registry.GetEntry(id);
                        var camLock = entry.Lock;
                        await camLock.WaitAsync(stoppingToken);
                        try
                        {
                            if (_registry.TryTakeRunner(id, out var runner))
                            {
                                var cameraName = entry.CameraName;

                                await _sessionLifecycle.StopAndDisposeRunnerAsync(id, runner!, "auto remove");
                                entry.CameraName = string.Empty;

                                _logger.LogInformation(
                                    "Camera session removed. CameraId={CameraId}",
                                    id);

                                await _statusNotifier.NotifyStatusAsync(id, cameraName ?? string.Empty, false, stoppingToken);
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
            _lifecycleState.MarkShuttingDown();

            var cameraIds = _registry.GetRunningCameraIds()
                .Concat(_registry.GetKnownCameraIds())
                .Distinct()
                .ToList();

            foreach (var cameraId in cameraIds)
            {
                var entry = _registry.GetEntry(cameraId);
                var camLock = entry.Lock;
                await camLock.WaitAsync(cancellationToken);
                try
                {
                    if (_registry.TryTakeRunner(cameraId, out var runner))
                    {
                        await _sessionLifecycle.StopAndDisposeRunnerAsync(cameraId, runner!, "application shutdown");
                        entry.CameraName = string.Empty;
                        entry.LastStatusSignature = null;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while stopping session. CameraId={CameraId}", cameraId);
                }
                finally
                {
                    camLock.Release();
                }
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
                await _statusNotifier.NotifyStatusAsync(cam.CameraId, cam.CameraName, true, token);
            }
        }

        private sealed class CameraRuntimeConfig
        {
            public int CameraId { get; set; }
            public string CameraName { get; set; } = string.Empty;
            public string RtspUrl { get; set; } = string.Empty;
        }
    }
}
