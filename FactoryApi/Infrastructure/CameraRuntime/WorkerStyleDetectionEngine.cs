using FactoryApi.Services.CameraRuntime;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace FactoryApi.Infrastructure.CameraRuntime
{
    public sealed class WorkerStyleDetectionEngine
    {
        private readonly CameraSessionState _state;
        private readonly ILogger _logger;
        private readonly ILabelDetector _labelDetector;

        public WorkerStyleDetectionEngine(
            CameraSessionState state,
            ILogger logger,
            ILabelDetector labelDetector)
        {
            _state = state;
            _logger = logger;
            _labelDetector = labelDetector;
        }

        public DetectionResult Analyze(
    Mat frame,
    Rect objectRect,
    Rect labelRect,
    bool checkRotation,
    bool checkLabel)
        {
            using var objectRoi = new Mat(frame, objectRect);
            using var labelRoi = new Mat(frame, labelRect);

            bool prevLabelInZone = _state.LabelInZone;
            bool prevCountedInCurrentRotation = _state.CountedInCurrentRotation;

            bool rotationActive = _state.RotationActive;
            bool rotationStarted = false;
            bool rotationEnded = false;
            double rotationChange = 0;
            double motionRatio = 0;

            if (checkRotation)
            {
                DetectRotation(
                    objectRoi,
                    out rotationActive,
                    out rotationStarted,
                    out rotationEnded,
                    out rotationChange,
                    out motionRatio);
            }

            bool detectorFound = false;
            float detectorConfidence = 0f;
            bool labelInZone = _state.LabelInZone;
            double labelChange = 0;

            if (checkLabel)
            {
                var detectResult = _labelDetector.Detect(labelRoi);

                // 필요 시 confidence 기준은 0.60 -> 0.35 정도로 완화 가능
                detectorFound = detectResult.Found && detectResult.Confidence >= 0.35f;
                detectorConfidence = detectResult.Confidence;

                DetectLabel(
                    labelRoi,
                    detectorFound,
                    out labelInZone,
                    out labelChange);
            }

            // 핵심: streak 기준 말고 "상태 변화" 자체로 enter/exit 판단
            bool labelEnter = !prevLabelInZone && labelInZone;
            bool labelExit = prevLabelInZone && !labelInZone;

            bool countAdded = false;

            const int COUNT_COOLDOWN_MS = 700;

            var now = DateTime.Now;
            double sinceLastCountMs = _state.LastProductionAt == DateTime.MinValue
                ? double.MaxValue
                : (now - _state.LastProductionAt).TotalMilliseconds;

            // 회전이 끝났으면 다음 회차 카운트 가능하게 초기화
            if (rotationEnded)
            {
                _state.CountedInCurrentRotation = false;
            }

            // 라벨이 빠졌으면 같은 회전 안에서도 다음 진입 카운트 허용
            if (labelExit)
            {
                _state.CountedInCurrentRotation = false;
            }

            // 회전 중 + 라벨 진입 + 아직 미카운트 + 쿨다운 경과
            if (rotationActive &&
                labelEnter &&
                !_state.CountedInCurrentRotation &&
                sinceLastCountMs >= COUNT_COOLDOWN_MS)
            {
                _state.ProductionCount++;
                _state.LastProductionAt = now;
                _state.CountedInCurrentRotation = true;
                countAdded = true;
            }

            _state.RotationActive = rotationActive;
            _state.LabelInZone = labelInZone;

            _state.LastStarted = rotationStarted;
            _state.LastEnded = rotationEnded;
            _state.LastLabelEnter = labelEnter;

            _state.LastRotationChangeValue = rotationChange;
            _state.LastMotionRatio = motionRatio;
            _state.LastLabelChangeValue = labelChange;

            _state.LastDetectorFound = detectorFound;
            _state.LastDetectorConfidence = detectorConfidence;
            _state.LastUpdatedAt = now;

            _logger.LogDebug(
                "Analyze | Rot={Rot} Started={Started} Ended={Ended} " +
                "PrevLabel={PrevLabel} LabelZone={LabelZone} Enter={Enter} Exit={Exit} " +
                "Detector={Detector} Conf={Conf:F2} OffStreak={OffStreak} " +
                "PrevCounted={PrevCounted} Counted={Counted} CountAdded={CountAdded} Count={Count}",
                rotationActive,
                rotationStarted,
                rotationEnded,
                prevLabelInZone,
                labelInZone,
                labelEnter,
                labelExit,
                detectorFound,
                detectorConfidence,
                _state.LabelOffStreak,
                prevCountedInCurrentRotation,
                _state.CountedInCurrentRotation,
                countAdded,
                _state.ProductionCount);

            return new DetectionResult
            {
                RotationActive = rotationActive,
                LabelInZone = labelInZone,
                RotationStarted = rotationStarted,
                RotationEnded = rotationEnded,
                LabelDetected = detectorFound,
                LabelEnter = labelEnter,
                CountAdded = countAdded,
                RotationChangeValue = rotationChange,
                MotionRatio = motionRatio,
                LabelChangeValue = labelChange,
                ProductionCount = _state.ProductionCount
            };
        }

        private void DetectRotation(
    Mat roiFrame,
    out bool rotationActive,
    out bool started,
    out bool ended,
    out double change,
    out double motionRatio)
        {
            const double startThreshold = 0.18;
            const double endThreshold = 0.10;

            const double motionRatioStartThreshold = 0.00005;
            const double motionRatioEndThreshold = 0.00002;

            const int minActiveFrames = 2;
            const int minEndFrames = 5;

            rotationActive = _state.RotationActive;
            started = false;
            ended = false;
            change = 0;
            motionRatio = 0;

            using var gray = new Mat();
            using var blur = new Mat();

            Cv2.CvtColor(roiFrame, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.GaussianBlur(gray, blur, new Size(5, 5), 0);

            if (_state.PrevRotationGray == null ||
                _state.PrevRotationGray.Empty() ||
                _state.PrevRotationGray.Size() != blur.Size())
            {
                _state.PrevRotationGray?.Dispose();
                _state.PrevRotationGray = blur.Clone();

                _state.RotationDetectedStreak = 0;
                _state.RotationOffStreak = 0;

                rotationActive = false;
                return;
            }

            using var diff = new Mat();
            using var motionMask = new Mat();

            Cv2.Absdiff(_state.PrevRotationGray, blur, diff);
            change = Cv2.Mean(diff).Val0;

            Cv2.Threshold(diff, motionMask, 10, 255, ThresholdTypes.Binary);
            double motionPixels = Cv2.CountNonZero(motionMask);
            motionRatio = motionPixels / (double)(motionMask.Rows * motionMask.Cols);

            bool onSignal = change >= startThreshold || motionRatio >= motionRatioStartThreshold;
            bool offSignal = change <= endThreshold && motionRatio <= motionRatioEndThreshold;

            if (!rotationActive)
            {
                if (onSignal)
                {
                    _state.RotationDetectedStreak++;
                    _state.RotationOffStreak = 0;

                    if (_state.RotationDetectedStreak >= minActiveFrames)
                    {
                        rotationActive = true;
                        started = true;
                    }
                }
                else
                {
                    _state.RotationDetectedStreak = 0;
                }
            }
            else
            {
                if (offSignal)
                {
                    _state.RotationOffStreak++;
                    _state.RotationDetectedStreak = 0;

                    if (_state.RotationOffStreak >= minEndFrames)
                    {
                        rotationActive = false;
                        ended = true;
                    }
                }
                else
                {
                    _state.RotationOffStreak = 0;
                }
            }

            _state.PrevRotationGray?.Dispose();
            _state.PrevRotationGray = blur.Clone();
        }

        private void DetectLabel(
    Mat labelRoiFrame,
    bool detectorFound,
    out bool labelDetectedInZone,
    out double labelChange)
        {
            const int detectMinFrames = 2;
            const int offMinFrames = 2;

            // 변화량은 디버그용/보조값만 사용
            const double changeOffThreshold = 0.10;

            labelDetectedInZone = _state.LabelInZone;
            labelChange = 0;

            using var gray = new Mat();
            using var blur = new Mat();

            Cv2.CvtColor(labelRoiFrame, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.GaussianBlur(gray, blur, new Size(5, 5), 0);

            if (_state.PrevLabelGray == null ||
                _state.PrevLabelGray.Empty() ||
                _state.PrevLabelGray.Size() != blur.Size())
            {
                _state.PrevLabelGray?.Dispose();
                _state.PrevLabelGray = blur.Clone();

                _state.LabelDetectedStreak = 0;
                _state.LabelOffStreak = 0;

                labelDetectedInZone = false;
                return;
            }

            using var diff = new Mat();
            Cv2.Absdiff(_state.PrevLabelGray, blur, diff);
            labelChange = Cv2.Mean(diff).Val0;

            // 핵심:
            // on 은 detectorFound 만 사용
            // off 는 detectorFound=false 가 연속되면 끔
            bool onSignal = detectorFound;
            bool offSignal = !detectorFound;

            if (onSignal)
            {
                _state.LabelDetectedStreak++;
                _state.LabelOffStreak = 0;

                if (_state.LabelDetectedStreak >= detectMinFrames)
                {
                    labelDetectedInZone = true;
                }
            }
            else if (offSignal)
            {
                _state.LabelDetectedStreak = 0;
                _state.LabelOffStreak++;

                if (_state.LabelOffStreak >= offMinFrames)
                {
                    labelDetectedInZone = false;
                }
            }

            _state.PrevLabelGray?.Dispose();
            _state.PrevLabelGray = blur.Clone();
        }
    }
}