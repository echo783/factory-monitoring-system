namespace FactoryApi.Services.CameraRuntime
{
    public sealed class DetectionResult
    {
        public bool RotationActive { get; set; } = false;
        public bool LabelInZone { get; set; } = false;

        public bool RotationStarted { get; set; } = false;
        public bool RotationEnded { get; set; } = false;

        public bool LabelDetected { get; set; } = false;
        public bool LabelEnter { get; set; } = false;

        public bool CountAdded { get; set; } = false;

        public double RotationChangeValue { get; set; } = 0;
        public double MotionRatio { get; set; } = 0;
        public double LabelChangeValue { get; set; } = 0;

        public int ProductionCount { get; set; } = 0;
    }
}