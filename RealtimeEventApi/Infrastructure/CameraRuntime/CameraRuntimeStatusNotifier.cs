using RealtimeEventApi.Contracts.Responses.Camera;

namespace RealtimeEventApi.Infrastructure.CameraRuntime
{
    public sealed class CameraRuntimeStatusNotifier
    {
        private readonly CameraRuntimeRegistry _registry;
        private readonly CameraRuntimeStatusFactory _statusFactory;
        private readonly ICameraStatusPublisher _statusPublisher;
        private readonly ILogger<CameraRuntimeStatusNotifier> _logger;

        public CameraRuntimeStatusNotifier(
            CameraRuntimeRegistry registry,
            CameraRuntimeStatusFactory statusFactory,
            ICameraStatusPublisher statusPublisher,
            ILogger<CameraRuntimeStatusNotifier> logger)
        {
            _registry = registry;
            _statusFactory = statusFactory;
            _statusPublisher = statusPublisher;
            _logger = logger;
        }

        public async Task NotifyStatusAsync(int cameraId, string cameraName, bool enabled, CancellationToken token)
        {
            var sessionExists = _registry.HasRunner(cameraId);
            var state = GetDebugStateSnapshot(cameraId);

            var effectiveCameraName = string.IsNullOrWhiteSpace(cameraName)
                ? _registry.GetEntry(cameraId).CameraName
                : cameraName;

            var status = _statusFactory.Create(
                cameraId,
                effectiveCameraName,
                enabled,
                sessionExists,
                state);

            await PublishIfChangedAsync(status, token);
        }

        public async Task SafeNotifyStatusAsync(int cameraId, string cameraName, bool enabled, CancellationToken token)
        {
            try
            {
                await NotifyStatusAsync(cameraId, cameraName, enabled, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish camera status. CameraId={CameraId}", cameraId);
            }
        }

        private async Task PublishIfChangedAsync(CameraRunStatusResponse status, CancellationToken token)
        {
            var entry = _registry.GetEntry(status.CameraId);
            var signature = CameraRuntimeStatusFactory.BuildSignature(status);

            if (entry.LastStatusSignature == signature)
                return;

            entry.LastStatusSignature = signature;
            await _statusPublisher.PublishAsync(status, token);
        }

        private CameraSessionSnapshot? GetDebugStateSnapshot(int cameraId)
        {
            return _registry.GetRunner(cameraId)?.GetDebugState();
        }
    }
}