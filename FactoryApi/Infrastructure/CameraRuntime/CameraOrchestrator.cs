using FactoryApi.Infrastructure.Persistence;
using FactoryApi.Services.CameraRuntime;
using Microsoft.EntityFrameworkCore;

namespace FactoryApi.Infrastructure.CameraRuntime
{
    public sealed class CameraOrchestrator : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<CameraOrchestrator> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly SnapshotFileService _snapshotFileService;
        private readonly ProductionPersistenceService _persistenceService;
        private readonly ILabelDetector _labelDetector;
        private readonly Dictionary<int, CameraSessionRunner> _sessions = new();
        private readonly SemaphoreSlim _sessionLock = new(1, 1);


        public CameraOrchestrator(
            IServiceScopeFactory scopeFactory,
            ILogger<CameraOrchestrator> logger,
            ILoggerFactory loggerFactory,
            SnapshotFileService snapshotFileService,
            ProductionPersistenceService persistenceService,
            ILabelDetector labelDetector)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _loggerFactory = loggerFactory;
            _snapshotFileService = snapshotFileService;
            _persistenceService = persistenceService;
            _labelDetector = labelDetector;
        }

        public CameraSessionState? GetDebugState(int cameraId)
        {
            if (_sessions.TryGetValue(cameraId, out var session))
                return session.GetDebugState();

            return null;
        }

        public bool IsRunning(int cameraId)
        {
            return _sessions.ContainsKey(cameraId);
        }

        public async Task<bool> StartCameraAsync(int cameraId, CancellationToken token = default)
        {
            await _sessionLock.WaitAsync(token);
            try
            {
                if (_sessions.ContainsKey(cameraId))
                    return true;

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
                    return false;

                var runner = new CameraSessionRunner(
                    _loggerFactory.CreateLogger<CameraSessionRunner>(),
                    _snapshotFileService,
                    _persistenceService,
                    cam.CameraId,
                    cam.CameraName,
                    cam.RtspUrl,
                    _labelDetector);

                _sessions.Add(cam.CameraId, runner);
                await runner.StartAsync(token);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "StartCameraAsync error. CameraId={CameraId}", cameraId);
                return false;
            }
            finally
            {
                _sessionLock.Release();
            }
        }

        public async Task<bool> StopCameraAsync(int cameraId, CancellationToken token = default)
        {
            await _sessionLock.WaitAsync(token);
            try
            {
                if (!_sessions.TryGetValue(cameraId, out var runner))
                    return true;

                await runner.StopAsync();
                await runner.DisposeAsync();
                _sessions.Remove(cameraId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "StopCameraAsync error. CameraId={CameraId}", cameraId);
                return false;
            }
            finally
            {
                _sessionLock.Release();
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

                    await _sessionLock.WaitAsync(stoppingToken);
                    try
                    {
                        foreach (var cam in cameraConfigs)
                        {
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

                            _sessions.Add(cam.CameraId, runner);
                            await runner.StartAsync(stoppingToken);

                            _logger.LogDebug(
                                "Camera session added. CameraId={CameraId}, CameraName={CameraName}, RtspUrl={RtspUrl}",
                                cam.CameraId,
                                cam.CameraName,
                                cam.RtspUrl);
                        }

                        var removedIds = _sessions.Keys
                            .Where(id => !currentIds.Contains(id))
                            .ToList();

                        foreach (var id in removedIds)
                        {
                            if (_sessions.TryGetValue(id, out var runner))
                            {
                                await runner.StopAsync();
                                await runner.DisposeAsync();
                                _sessions.Remove(id);

                                _logger.LogInformation(
                                    "Camera session removed. CameraId={CameraId}",
                                    id);
                            }
                        }
                    }
                    finally
                    {
                        _sessionLock.Release();
                    }
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
        public CameraSessionRunner? GetSession(int cameraId)
        {
            _sessions.TryGetValue(cameraId, out var session);
            return session;
        }
        private sealed class CameraRuntimeConfig
        {
            public int CameraId { get; set; }
            public string CameraName { get; set; } = string.Empty;
            public string RtspUrl { get; set; } = string.Empty;
        }
    }
}