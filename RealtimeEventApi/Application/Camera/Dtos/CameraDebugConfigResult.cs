namespace RealtimeEventApi.Application.Camera.Dtos
{
    public class CameraDebugConfigResult
    {
        public int CameraId { get; set; }
        public string? CameraName { get; set; }
        public string? ProductName { get; set; }
        public bool CheckRotation { get; set; }
        public bool CheckLabel { get; set; }

        public Double ObjectRoiX { get; set; }
        public Double ObjectRoiY { get; set; }
        public Double ObjectRoiW { get; set; }
        public Double ObjectRoiH { get; set; }

        public Double LabelRoiX { get; set; }
        public Double LabelRoiY { get; set; }
        public Double LabelRoiW { get; set; }
        public Double LabelRoiH { get; set; }

        public string? LatestUrl { get; set; }
    }
}
