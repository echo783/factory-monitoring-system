namespace RealtimeEventApi.Infrastructure.CameraRuntime
{
    public sealed class CameraRuntimeSessionLifecycle
    {
        private readonly ILogger<CameraRuntimeSessionLifecycle> _logger;

        public CameraRuntimeSessionLifecycle(
            ILogger<CameraRuntimeSessionLifecycle> logger)
        {
            _logger = logger;
        }

        public async Task StopAndDisposeRunnerAsync(
            int cameraId,
            CameraSessionRunner runner,
            string operation)
        {
            try
            {
                await runner.StopAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while stopping runner. Operation={Operation}, CameraId={CameraId}", operation, cameraId);
            }

            try
            {
                await runner.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while disposing runner. Operation={Operation}, CameraId={CameraId}", operation, cameraId);
            }
        }
    }
}