using OpenCvSharp;

namespace FactoryApi.Services.CameraRuntime
{
    public sealed class CameraSessionState : IDisposable
    {
        // ===== latest / count 시각 =====
        public DateTime LastLatestSavedAt { get; set; } = DateTime.MinValue;
        public DateTime LastProductionAt { get; set; } = DateTime.MinValue;
        public DateTime RotationStartTime { get; set; } = DateTime.MinValue;

        // ===== 이전 프레임 =====
        public Mat? PrevRotationGray { get; set; }
        public Mat? PrevLabelGray { get; set; }

        // ===== 현재 상태 =====
        public bool RotationActive { get; set; }
        public bool LabelInZone { get; set; }
        public bool CountedInCurrentRotation { get; set; }

        // ===== 마지막 계산값 =====
        public double LastRotationChangeValue { get; set; }
        public double LastMotionRatio { get; set; }
        public double LastLabelChangeValue { get; set; }

        // ===== 마지막 이벤트 상태 =====
        public bool LastStarted { get; set; }
        public bool LastEnded { get; set; }
        public bool LastLabelEnter { get; set; }

        // ===== 라벨 detector 상태 =====
        public bool LastDetectorFound { get; set; }
        public float LastDetectorConfidence { get; set; }

        // ===== 라벨 streak =====
        public int LabelDetectedStreak { get; set; }
        public int LabelOffStreak { get; set; }

        // ===== 생산량 =====
        public int ProductionCount { get; set; }

        // ===== 스트림 상태 =====
        public DateTime LastFrameAt { get; set; } = DateTime.MinValue;
        public DateTime LastSuccessfulReadAt { get; set; } = DateTime.MinValue;
        public DateTime LastReconnectAt { get; set; } = DateTime.MinValue;
        public DateTime SessionStartedAt { get; set; } = DateTime.Now;
        public DateTime LastUpdatedAt { get; set; } = DateTime.MinValue;

        public int ConsecutiveReadFails { get; set; }
        public int ConsecutiveSameFrameCount { get; set; }
        public ulong LastFrameSignature { get; set; }

        public bool StreamJustReconnected { get; set; }
        public int RotationDetectedStreak { get; set; }
        public int RotationOffStreak { get; set; }
        public void ResetAnalysisFrames()
        {
            PrevRotationGray?.Dispose();
            PrevRotationGray = null;

            PrevLabelGray?.Dispose();
            PrevLabelGray = null;

            RotationActive = false;
            LabelInZone = false;
            CountedInCurrentRotation = false;

            LastRotationChangeValue = 0;
            LastMotionRatio = 0;
            LastLabelChangeValue = 0;

            LastStarted = false;
            LastEnded = false;
            LastLabelEnter = false;

            LastDetectorFound = false;
            LastDetectorConfidence = 0f;

            LabelDetectedStreak = 0;
            LabelOffStreak = 0;

            RotationStartTime = DateTime.MinValue;

            var now = DateTime.Now;
            SessionStartedAt = now;
            LastUpdatedAt = now;
            RotationDetectedStreak = 0;
            RotationOffStreak = 0;
        }

        public void Dispose()
        {
            PrevRotationGray?.Dispose();
            PrevRotationGray = null;

            PrevLabelGray?.Dispose();
            PrevLabelGray = null;
        }
    }
}