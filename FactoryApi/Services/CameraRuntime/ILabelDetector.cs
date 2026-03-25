using OpenCvSharp;

namespace FactoryApi.Services.CameraRuntime
{
    public interface ILabelDetector
    {
        DetectedLabelResult Detect(Mat labelRoi);
    }
}