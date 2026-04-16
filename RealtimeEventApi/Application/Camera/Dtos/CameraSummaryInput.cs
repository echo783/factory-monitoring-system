namespace RealtimeEventApi.Application.Camera.Dtos
{
    public class CameraSummaryInput
    {
        // 기본 정보
        public int CameraId { get; set; }
        public string CameraName { get; set; } = "";
        public string ProductName { get; set; } = "";

        // 기능 설정
        public bool CheckRotation { get; set; }
        public bool CheckLabel { get; set; }

        // ROI 상태
        public bool IsRoiConfigured { get; set; } // ROI 값이 입력되어 있는지
        public bool IsRoiValid { get; set; }      // ROI 값이 정상 범위인지

        // 운영 상태
        public bool IsConnected { get; set; }
        public DateTime? LastFrameAt { get; set; }
        public DateTime? LastReconnectAt { get; set; }
        public int RecentProductionCount { get; set; }
        public int RecentLabelMissCount { get; set; }

        // 보조 정보
        public string? LatestImageUrl { get; set; }
        public bool ImageExists { get; set; }
    }
}