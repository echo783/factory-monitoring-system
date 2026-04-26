namespace RealtimeEventApi.Infrastructure.CameraRuntime
{
    public sealed class CameraRuntimeLifecycleState
    {
        private volatile bool _isShuttingDown;

        public bool IsShuttingDown => _isShuttingDown;

        public void MarkShuttingDown()
        {
            _isShuttingDown = true;
        }
    }
}
