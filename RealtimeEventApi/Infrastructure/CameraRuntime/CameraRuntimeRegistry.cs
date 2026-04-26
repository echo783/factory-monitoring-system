using System.Collections.Concurrent;

namespace RealtimeEventApi.Infrastructure.CameraRuntime
{
    public sealed class CameraRuntimeRegistry
    {
        private readonly ConcurrentDictionary<int, CameraRuntimeEntry> _entries = new();

        internal CameraRuntimeEntry GetEntry(int cameraId)
        {
            return _entries.GetOrAdd(cameraId, _ => new CameraRuntimeEntry());
        }

        internal bool TrySetRunner(int cameraId, CameraSessionRunner runner)
        {
            // Caller must hold entry.Lock.
            var entry = GetEntry(cameraId);
            if (entry.Runner != null)
                return false;

            entry.Runner = runner;
            return true;
        }

        internal bool TryTakeRunner(int cameraId, out CameraSessionRunner? runner)
        {
            // Caller must hold entry.Lock.
            var entry = GetEntry(cameraId);
            runner = entry.Runner;
            if (runner == null)
                return false;

            entry.Runner = null;
            return true;
        }

        internal CameraSessionRunner? GetRunner(int cameraId)
        {
            return GetEntry(cameraId).Runner;
        }

        internal bool HasRunner(int cameraId)
        {
            return GetEntry(cameraId).Runner != null;
        }

        internal List<int> GetRunningCameraIds()
        {
            return _entries
                .Where(pair => pair.Value.Runner != null)
                .Select(pair => pair.Key)
                .ToList();
        }

        internal List<int> GetKnownCameraIds()
        {
            return _entries.Keys.ToList();
        }
    }

    internal sealed class CameraRuntimeEntry
    {
        public SemaphoreSlim Lock { get; } = new(1, 1);
        public CameraSessionRunner? Runner { get; set; }
        public string CameraName { get; set; } = string.Empty;
        public string? LastStatusSignature { get; set; }
    }
}
