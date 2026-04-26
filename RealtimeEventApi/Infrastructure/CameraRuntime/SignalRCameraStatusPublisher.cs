using RealtimeEventApi.Contracts.Responses.Camera;
using RealtimeEventApi.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace RealtimeEventApi.Infrastructure.CameraRuntime
{
    public sealed class SignalRCameraStatusPublisher : ICameraStatusPublisher
    {
        private readonly IHubContext<CameraHub> _hubContext;

        public SignalRCameraStatusPublisher(IHubContext<CameraHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public Task PublishAsync(CameraRunStatusResponse status, CancellationToken token = default)
        {
            return _hubContext.Clients
                .Groups($"camera-{status.CameraId}", CameraHub.DashboardGroup)
                .SendAsync("CameraStatusChanged", status, token);
        }
    }
}
