namespace RealtimeEventApi.Infrastructure.CameraRuntime
{
    public sealed class CameraRuntimeReader : ICameraRuntimeReader
    {
        private readonly CameraRuntimeRegistry _registry;

        public CameraRuntimeReader(CameraRuntimeRegistry registry)
        {
            _registry = registry;
        }

        public int GetCameraCount()
        {
            return _registry.GetRunningCameraIds().Count;
        }

        public CameraSessionSnapshot? GetDebugState(int cameraId)
        {
            return _registry.GetRunner(cameraId)?.GetDebugState();
        }
    }
}
