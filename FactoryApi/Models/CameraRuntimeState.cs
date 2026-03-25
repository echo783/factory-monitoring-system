using OpenCvSharp;

namespace FactoryApi.Services.CameraRuntime
{
    public sealed class CameraRuntimeState
    {
        // 이전 프레임 저장
        public Mat? PrevRotationGray { get; set; }
        public Mat? PrevLabelGray { get; set; }

        // 현재 상태
        public bool RotationActive { get; set; }
        public bool LabelInZone { get; set; }
        public bool CountedInCurrentRotation { get; set; }

        // 생산량
        public int ProductionCount { get; set; }

        // 마지막 시간 정보
        public DateTime LastProductionAt { get; set; } = DateTime.MinValue;
        public DateTime LastUpdatedAt { get; set; } = DateTime.MinValue;

        // 마지막 계산값
        public double LastRotationChangeValue { get; set; }
        public double LastMotionRatio { get; set; }
        public double LastLabelChangeValue { get; set; }

        // 마지막 이벤트 상태
        public bool LastStarted { get; set; }
        public bool LastEnded { get; set; }
        public bool LastLabelEnter { get; set; }

        // 디버그/보조용 streak
        public int LabelDetectedStreak { get; set; }
        public int LabelOffStreak { get; set; }
    }
}