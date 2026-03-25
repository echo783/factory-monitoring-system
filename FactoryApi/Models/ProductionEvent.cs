using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FactoryApi.Models
{
    [Table("ProductionEvent")]
    public class ProductionEvent
    {
        [Key]
        public long EventId { get; set; }

        public int CameraId { get; set; }

        [Required]
        [MaxLength(100)]
        public string ProductName { get; set; } = string.Empty;

        public DateTime EventTime { get; set; }

        public int ProductionCount { get; set; }

        [MaxLength(500)]
        public string? SnapshotPath { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}