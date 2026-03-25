namespace FactoryClient.Models
{
    public class DebugStateDto
    {
        public bool RotationActive { get; set; }
        public bool LabelInZone { get; set; }
        public bool CountedInCurrentRotation { get; set; }

        public bool LastStarted { get; set; }
        public bool LastEnded { get; set; }
        public bool LastLabelEnter { get; set; }

        public bool LastDetectorFound { get; set; }
        public float LastDetectorConfidence { get; set; }

        public int LabelDetectedStreak { get; set; }
        public int LabelOffStreak { get; set; }

        public double LastRotationChangeValue { get; set; }
        public double LastMotionRatio { get; set; }
        public double LastLabelChangeValue { get; set; }

        public int ProductionCount { get; set; }
        public string LastUpdatedAt { get; set; } = "";
    }
}