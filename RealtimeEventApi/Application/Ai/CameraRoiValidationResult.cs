namespace FactoryApi.Application.Ai
{
    public sealed class CameraRoiValidationResult
    {
        public bool CameraExists { get; set; }
        public bool ImageExists { get; set; }
        public bool Success { get; set; }

        public bool ObjectDetected { get; set; }
        public double ObjectConfidence { get; set; }
        public int ObjectCount { get; set; }
        public List<string> ObjectClasses { get; set; } = new();

        public bool LabelDetected { get; set; }
        public double LabelConfidence { get; set; }
        public int LabelCount { get; set; }
        public List<string> LabelTexts { get; set; } = new();
        public bool LabelKeywordFound { get; set; }

        public string Message { get; set; } = string.Empty;
        public string ImagePath { get; set; } = string.Empty;
    }
}