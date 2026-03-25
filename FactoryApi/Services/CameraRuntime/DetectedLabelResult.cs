using OpenCvSharp;

namespace FactoryApi.Services.CameraRuntime
{
    public sealed class DetectedLabelResult
    {
        public bool Found { get; set; } = false;
        public float Confidence { get; set; } = 0f;
        public Rect BoundingBox { get; set; } = default;
    }
}