namespace FactoryApi.Models
{
    public sealed class DebugDto
    {
        public bool RotationActive { get; set; }
        public bool LabelInZone { get; set; }

        public bool LastStarted { get; set; }
        public bool LastEnded { get; set; }
        public bool LastLabelEnter { get; set; }

        public double LastRotationChangeValue { get; set; }
        public double LastMotionRatio { get; set; }
        public double LastLabelChangeValue { get; set; }

        public int ProductionCount { get; set; }

        public DateTime? LastProductionAt { get; set; }
    }
}