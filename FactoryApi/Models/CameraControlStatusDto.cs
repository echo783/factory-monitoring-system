namespace FactoryApi.Models
{
    public class CameraControlStatusDto
    {
        public int CameraId { get; set; }
        public string CameraName { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public string Status { get; set; } = string.Empty;   // Running / Stopped / Error
        public string Message { get; set; } = string.Empty;
        public DateTime ChangedAt { get; set; }
    }
}