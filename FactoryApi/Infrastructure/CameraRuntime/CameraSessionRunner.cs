using FactoryApi.Models;
using FactoryApi.Services.CameraRuntime;
using OpenCvSharp;

namespace FactoryApi.Infrastructure.CameraRuntime
{
    public sealed class CameraSessionRunner : IAsyncDisposable
    {
        private readonly ILogger<CameraSessionRunner> _logger;
        private readonly ProductionPersistenceService _persistenceService;
        private readonly SnapshotFileService _snapshotFileService;
        private readonly int _cameraId;
        private readonly string _cameraName;
        private readonly string _streamUrl;

        private readonly LatestFrameBuffer _buffer = new();
        private readonly CameraSessionState _state = new();
        private readonly WorkerStyleDetectionEngine _engine;

        private CancellationTokenSource? _cts;
        private Task? _readTask;

        private CameraConfig? _cameraConfig;
        private DateTime _lastConfigLoadedAt = DateTime.MinValue;

        private readonly ILabelDetector _labelDetector;

        public CameraSessionRunner(
    ILogger<CameraSessionRunner> logger,
    SnapshotFileService snapshotFileService,
    ProductionPersistenceService persistenceService,
    int cameraId,
    string cameraName,
    string streamUrl,
    ILabelDetector labelDetector)
        {
            _logger = logger;
            _snapshotFileService = snapshotFileService;
            _persistenceService = persistenceService;

            _cameraId = cameraId;
            _cameraName = cameraName;
            _streamUrl = streamUrl;
            _labelDetector = labelDetector;

            _engine = new WorkerStyleDetectionEngine(_state, logger, _labelDetector);
        }

        public Task StartAsync(CancellationToken outerToken)
        {
            if (_cts != null)
                return Task.CompletedTask;

            _cts = CancellationTokenSource.CreateLinkedTokenSource(outerToken);

            _state.SessionStartedAt = DateTime.Now;
            _readTask = Task.Run(() => ReadLoopAsync(_cts.Token), _cts.Token);

            _logger.LogInformation(
                "Camera session started. CameraId={CameraId}, Name={Name}, Url={Url}",
                _cameraId, _cameraName, _streamUrl);

            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            if (_cts == null)
                return;

            try
            {
                _cts.Cancel();

                if (_readTask != null)
                    await _readTask;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while stopping camera session. CameraId={CameraId}", _cameraId);
            }
            finally
            {
                _cts.Dispose();
                _cts = null;
                _readTask = null;
            }
        }

        private async Task<CameraConfig?> EnsureCameraConfigAsync(CancellationToken token)
        {
            if (_cameraConfig != null &&
                (DateTime.Now - _lastConfigLoadedAt).TotalSeconds < 5)
            {
                return _cameraConfig;
            }

            _cameraConfig = await _persistenceService.GetCameraConfigAsync(_cameraId, token);
            _lastConfigLoadedAt = DateTime.Now;
            return _cameraConfig;
        }

        private void ResetAnalysisStateForReconnect()
        {
            _state.ResetAnalysisFrames();
            _state.ConsecutiveReadFails = 0;
            _state.ConsecutiveSameFrameCount = 0;
            _state.LastFrameSignature = 0;
            _state.StreamJustReconnected = true;
            _state.LastReconnectAt = DateTime.Now;
            _state.SessionStartedAt = DateTime.Now;
        }

        private static ulong ComputeFrameSignature(Mat frame)
        {
            using var small = new Mat();
            using var gray = new Mat();

            Cv2.Resize(frame, small, new Size(32, 18));
            Cv2.CvtColor(small, gray, ColorConversionCodes.BGR2GRAY);

            ulong hash = 1469598103934665603UL;
            for (int y = 0; y < gray.Rows; y++)
            {
                for (int x = 0; x < gray.Cols; x++)
                {
                    byte v = gray.At<byte>(y, x);
                    hash ^= v;
                    hash *= 1099511628211UL;
                }
            }

            return hash;
        }

        private async Task ReadLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                VideoCapture? cap = null;

                try
                {
                    cap = new VideoCapture();

                    string openUrl = _streamUrl.Contains("?")
                        ? _streamUrl + "&rtsp_transport=tcp"
                        : _streamUrl + "?rtsp_transport=tcp";

                    _logger.LogInformation("Opening stream... CameraId={CameraId}, Url={Url}", _cameraId, openUrl);

                    if (!cap.Open(openUrl, VideoCaptureAPIs.FFMPEG) || !cap.IsOpened())
                    {
                        _logger.LogWarning("Failed to open stream. CameraId={CameraId}, Url={Url}", _cameraId, openUrl);
                        await Task.Delay(1500, token);
                        continue;
                    }

                    cap.Set(VideoCaptureProperties.BufferSize, 1);

                    ResetAnalysisStateForReconnect();
                    await Task.Delay(1500, token);

                    for (int i = 0; i < 10; i++)
                    {
                        using var warmup = new Mat();
                        cap.Read(warmup);
                        await Task.Delay(30, token);
                    }

                    using var frame = new Mat();

                    while (!token.IsCancellationRequested && cap.IsOpened())
                    {
                        bool ok = cap.Read(frame);

                        if (!ok || frame.Empty())
                        {
                            _state.ConsecutiveReadFails++;

                            if (_state.ConsecutiveReadFails < 15)
                            {
                                await Task.Delay(50, token);
                                continue;
                            }

                            _logger.LogWarning(
                                "Read failed repeatedly. Reconnecting... CameraId={CameraId}, FailCount={FailCount}",
                                _cameraId,
                                _state.ConsecutiveReadFails);

                            break;
                        }

                        _state.ConsecutiveReadFails = 0;
                        _state.LastSuccessfulReadAt = DateTime.Now;
                        _state.LastFrameAt = DateTime.Now;

                        ulong sig = ComputeFrameSignature(frame);
                        if (sig == _state.LastFrameSignature)
                        {
                            _state.ConsecutiveSameFrameCount++;
                        }
                        else
                        {
                            _state.ConsecutiveSameFrameCount = 0;
                            _state.LastFrameSignature = sig;
                        }

                        if (_state.ConsecutiveSameFrameCount >= 50)
                        {
                            _logger.LogWarning(
                                "Stale frame detected. Reconnecting... CameraId={CameraId}, SameFrameCount={SameFrameCount}",
                                _cameraId,
                                _state.ConsecutiveSameFrameCount);

                            break;
                        }

                        _buffer.Set(frame);
                        _state.StreamJustReconnected = false;

                        using var analyzeFrame = frame.Clone();
                        await AnalyzeFrameAsync(analyzeFrame, DateTime.Now, token);

                        await Task.Delay(30, token);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ReadLoop error. CameraId={CameraId}", _cameraId);
                    await Task.Delay(1500, token);
                }
                finally
                {
                    try
                    {
                        cap?.Release();
                        cap?.Dispose();
                    }
                    catch
                    {
                    }
                }

                await Task.Delay(500, token);
            }
        }

        private async Task AnalyzeFrameAsync(Mat frame, DateTime capturedAt, CancellationToken token)
        {
            try
            {
                if ((DateTime.Now - _state.LastLatestSavedAt).TotalMilliseconds >= 1000)
                {
                    await _snapshotFileService.SaveLatestAsync(_cameraId, frame, token);
                    _state.LastLatestSavedAt = DateTime.Now;
                }

                var config = await EnsureCameraConfigAsync(token);
                if (config == null)
                {
                    _logger.LogWarning("CameraConfig not found. CameraId={CameraId}", _cameraId);
                    return;
                }

                var (objectRect, labelRect) = RoiRectHelper.BuildFromCameraConfig(config, frame.Width, frame.Height);

                if (objectRect.Width <= 0 || objectRect.Height <= 0 ||
                    labelRect.Width <= 0 || labelRect.Height <= 0)
                {
                    _logger.LogWarning(
                        "ROI rect invalid. CameraId={CameraId}, Obj=({OX},{OY},{OW},{OH}), Label=({LX},{LY},{LW},{LH})",
                        _cameraId,
                        objectRect.X, objectRect.Y, objectRect.Width, objectRect.Height,
                        labelRect.X, labelRect.Y, labelRect.Width, labelRect.Height);
                    return;
                }

                DetectionResult result = _engine.Analyze(
                    frame,
                    objectRect,
                    labelRect,
                    config.CheckRotation,
                    config.CheckLabel);

                if (!result.CountAdded)
                    return;

                string? snapshotPath = await _snapshotFileService.SaveEventSnapshotAsync(
                    _cameraId, frame, capturedAt, token);

                await _persistenceService.SaveProductionEventAsync(
                    _cameraId,
                    config.ProductName,
                    capturedAt,
                    _state.ProductionCount,
                    snapshotPath,
                    token);

                _logger.LogDebug(
                    "COUNT HIT! CameraId={CameraId}, ProductName={ProductName}, Count={Count}",
                    _cameraId,
                    config.ProductName,
                    _state.ProductionCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AnalyzeFrameAsync error. CameraId={CameraId}", _cameraId);
            }
        }
        public void ResetAnalysisState()
        {
            _state.ResetAnalysisFrames();
        }
        public CameraSessionState GetDebugState() => _state;

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            _state.Dispose();
            _buffer.Dispose();
        }
    }
}