namespace RealtimeEventApi.Contracts.Responses.Camera
{
    public class CameraRunStatusResponse
    {
        public int CameraId { get; set; }
        public string CameraName { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public string Status { get; set; } = string.Empty;   // Running / Connecting / Stale / Starting / Stopping / Stopped / Error
        public string Message { get; set; } = string.Empty;
        public DateTime ChangedAt { get; set; }
        public DateTime? LastSuccessfulReadAt { get; set; }
        public DateTime? LastErrorAt { get; set; }
        public string LastErrorMessage { get; set; } = string.Empty;
        public int ProductionCount { get; set; }
    }
}
