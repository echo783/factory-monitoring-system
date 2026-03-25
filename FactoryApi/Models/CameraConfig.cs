using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FactoryApi.Models
{
    [Table("CameraConfig")]
    public class CameraConfig
    {
        [Key]
        public int CameraId { get; set; }

        [Required]
        [MaxLength(100)]
        public string CameraName { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string RtspUrl { get; set; } = string.Empty;

        public bool Enabled { get; set; }

        [Required]
        [MaxLength(100)]
        public string ProductName { get; set; } = string.Empty;

        public double ObjectRoiX { get; set; }
        public double ObjectRoiY { get; set; }
        public double ObjectRoiW { get; set; }
        public double ObjectRoiH { get; set; }

        public double LabelRoiX { get; set; }
        public double LabelRoiY { get; set; }
        public double LabelRoiW { get; set; }
        public double LabelRoiH { get; set; }

        public bool CheckRotation { get; set; }
        public bool CheckLabel { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}