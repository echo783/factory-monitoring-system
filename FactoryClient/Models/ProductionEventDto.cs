namespace FactoryClient.Models
{
    public class ProductionEventDto
    {
        public long Id { get; set; }
        public int CameraId { get; set; }
        public string CameraName { get; set; } = "";
        public string ProductName { get; set; } = "";
        public DateTime EventTime { get; set; }
        public int ProductionCount { get; set; }
        public string ImagePath { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }
}