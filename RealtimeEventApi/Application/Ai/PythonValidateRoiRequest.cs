using System.Text.Json.Serialization;

namespace FactoryApi.Application.Ai
{
    public sealed class PythonValidateRoiRequest
    {
        [JsonPropertyName("imagePath")]
        public string ImagePath { get; set; } = string.Empty;

        [JsonPropertyName("objectX")]
        public double ObjectX { get; set; }

        [JsonPropertyName("objectY")]
        public double ObjectY { get; set; }

        [JsonPropertyName("objectW")]
        public double ObjectW { get; set; }

        [JsonPropertyName("objectH")]
        public double ObjectH { get; set; }

        [JsonPropertyName("labelX")]
        public double LabelX { get; set; }

        [JsonPropertyName("labelY")]
        public double LabelY { get; set; }

        [JsonPropertyName("labelW")]
        public double LabelW { get; set; }

        [JsonPropertyName("labelH")]
        public double LabelH { get; set; }
    }
}