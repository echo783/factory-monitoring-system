using Microsoft.AspNetCore.SignalR;

namespace RealtimeEventApi.Hubs
{
    public class CameraHub : Hub
    {
        public const string DashboardGroup = "camera-dashboard";

        public Task JoinCameraGroup(int cameraId)
        {
            return Groups.AddToGroupAsync(Context.ConnectionId, $"camera-{cameraId}");
        }

        public Task LeaveCameraGroup(int cameraId)
        {
            return Groups.RemoveFromGroupAsync(Context.ConnectionId, $"camera-{cameraId}");
        }

        public Task JoinDashboardGroup()
        {
            return Groups.AddToGroupAsync(Context.ConnectionId, DashboardGroup);
        }

        public Task LeaveDashboardGroup()
        {
            return Groups.RemoveFromGroupAsync(Context.ConnectionId, DashboardGroup);
        }
    }
}
