using OpenCvSharp;

namespace FactoryApi.Services.CameraRuntime
{
    public sealed class LatestFrameBuffer : IDisposable
    {
        private readonly object _lock = new();
        private Mat? _latestFrame;
        private DateTime _capturedAt = DateTime.MinValue;

        public void Set(Mat source)
        {
            lock (_lock)
            {
                _latestFrame?.Dispose();
                _latestFrame = source.Clone();
                _capturedAt = DateTime.Now;
            }
        }

        public bool TryGet(out Mat? frame, out DateTime capturedAt)
        {
            lock (_lock)
            {
                if (_latestFrame == null || _latestFrame.Empty())
                {
                    frame = null;
                    capturedAt = DateTime.MinValue;
                    return false;
                }

                frame = _latestFrame.Clone();
                capturedAt = _capturedAt;
                return true;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _latestFrame?.Dispose();
                _latestFrame = null;
                _capturedAt = DateTime.MinValue;
            }
        }

        public void Dispose()
        {
            Clear();
        }
    }
}