using RealtimeEventApi.Infrastructure.Persistence;

namespace RealtimeEventApi.Infrastructure.CameraRuntime
{
    public sealed class CameraSessionRunnerFactory
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly SnapshotFileService _snapshotFileService;
        private readonly ProductionPersistenceService _persistenceService;
        private readonly ILabelDetector _labelDetector;

        public CameraSessionRunnerFactory(
            ILoggerFactory loggerFactory,
            SnapshotFileService snapshotFileService,
            ProductionPersistenceService persistenceService,
            ILabelDetector labelDetector)
        {
            _loggerFactory = loggerFactory;
            _snapshotFileService = snapshotFileService;
            _persistenceService = persistenceService;
            _labelDetector = labelDetector;
        }

        public CameraSessionRunner Create(int cameraId, string cameraName, string rtspUrl)
        {
            return new CameraSessionRunner(
                _loggerFactory.CreateLogger<CameraSessionRunner>(),
                _snapshotFileService,
                _persistenceService,
                cameraId,
                cameraName,
                rtspUrl,
                _labelDetector);
        }
    }
}
