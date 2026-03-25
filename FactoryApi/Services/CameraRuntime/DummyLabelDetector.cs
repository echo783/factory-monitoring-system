using OpenCvSharp;

namespace FactoryApi.Services.CameraRuntime
{
    public sealed class DummyLabelDetector : ILabelDetector
    {
        public DetectedLabelResult Detect(Mat roi)
        {
            if (roi == null || roi.Empty())
            {
                return new DetectedLabelResult
                {
                    Found = false,
                    Confidence = 0f
                };
            }

            using var hsv = new Mat();
            using var maskBlue = new Mat();

            Cv2.CvtColor(roi, hsv, ColorConversionCodes.BGR2HSV);

            Scalar lowerBlue = new Scalar(90, 60, 40);
            Scalar upperBlue = new Scalar(140, 255, 255);

            Cv2.InRange(hsv, lowerBlue, upperBlue, maskBlue);

            int bluePixels = Cv2.CountNonZero(maskBlue);
            int totalPixels = roi.Rows * roi.Cols;

            double ratio = totalPixels <= 0 ? 0 : (double)bluePixels / totalPixels;

            bool found = ratio >= 0.015;

            return new DetectedLabelResult
            {
                Found = found,
                Confidence = (float)Math.Min(1.0, ratio * 20.0)
            };
        }
    }
}