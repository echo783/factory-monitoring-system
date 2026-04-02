using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace FactoryApi.Infrastructure.MediaMtx
{
    public class MediaMtxService : BackgroundService
    {
        private readonly ILogger<MediaMtxService> _logger;
        private readonly MediaMtxOptions _options;
        private readonly MediaMtxConfigWriter _configWriter;

        private Process? _process;
        private readonly object _sync = new();

        public MediaMtxService(
            ILogger<MediaMtxService> logger,
            IOptions<MediaMtxOptions> options,
            MediaMtxConfigWriter configWriter)
        {
            _logger = logger;
            _options = options.Value;
            _configWriter = configWriter;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation("MediaMTX is disabled by configuration.");
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (!IsProcessRunning())
                    {
                        await _configWriter.WriteConfigAsync(stoppingToken);
                        KillExistingMediaMtxProcesses();
                        StartMediaMtx();
                    }

                    await Task.Delay(1000, stoppingToken);

                    if (_process != null && _process.HasExited)
                    {
                        _logger.LogWarning("MediaMTX exited. ExitCode={ExitCode}", _process.ExitCode);

                        CleanupProcess();

                        if (!_options.AutoRestart)
                        {
                            _logger.LogWarning("AutoRestart disabled. MediaMTX will not restart.");
                            break;
                        }

                        _logger.LogInformation("Restarting MediaMTX after {Delay} ms...", _options.RestartDelayMs);
                        await Task.Delay(_options.RestartDelayMs, stoppingToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while monitoring MediaMTX.");

                    if (!_options.AutoRestart)
                        break;

                    await Task.Delay(_options.RestartDelayMs, stoppingToken);
                }
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping MediaMTX service...");

            await base.StopAsync(cancellationToken);

            StopMediaMtx();
        }

        private void StartMediaMtx()
        {
            lock (_sync)
            {
                if (IsProcessRunning())
                    return;

                string baseDir = AppContext.BaseDirectory;
                string exePath = Path.GetFullPath(Path.Combine(baseDir, _options.ExeRelativePath));
                string configPath = Path.GetFullPath(Path.Combine(baseDir, _options.ConfigRelativePath));
                string workingDir = Path.GetDirectoryName(exePath) ?? baseDir;

                if (!File.Exists(exePath))
                {
                    _logger.LogError("MediaMTX exe not found: {Path}", exePath);
                    return;
                }

                if (!File.Exists(configPath))
                {
                    _logger.LogError("MediaMTX config not found: {Path}", configPath);
                    return;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = $"\"{configPath}\"",
                    WorkingDirectory = workingDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                var process = new Process
                {
                    StartInfo = psi,
                    EnableRaisingEvents = true
                };

                process.OutputDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                        _logger.LogInformation("[MediaMTX] {Line}", e.Data);
                };

                process.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                        _logger.LogError("[MediaMTX] {Line}", e.Data);
                };

                process.Exited += (_, _) =>
                {
                    try
                    {
                        _logger.LogWarning("MediaMTX process exited.");
                    }
                    catch
                    {
                    }
                };

                bool started = process.Start();
                if (!started)
                {
                    _logger.LogError("Failed to start MediaMTX.");
                    process.Dispose();
                    return;
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                _process = process;

                _logger.LogInformation("MediaMTX started. PID={Pid}", process.Id);
                _logger.LogInformation("MediaMTX exe : {ExePath}", exePath);
                _logger.LogInformation("MediaMTX yml : {ConfigPath}", configPath);
            }
        }

        private void StopMediaMtx()
        {
            lock (_sync)
            {
                if (_process == null)
                    return;

                try
                {
                    if (!_process.HasExited)
                    {
                        _logger.LogInformation("Killing MediaMTX process. PID={Pid}", _process.Id);
                        _process.Kill(entireProcessTree: true);
                        _process.WaitForExit(3000);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to stop MediaMTX process.");
                }
                finally
                {
                    CleanupProcess();
                }
            }
        }

        private bool IsProcessRunning()
        {
            lock (_sync)
            {
                return _process != null && !_process.HasExited;
            }
        }

        private void CleanupProcess()
        {
            lock (_sync)
            {
                try
                {
                    _process?.Dispose();
                }
                catch
                {
                }
                finally
                {
                    _process = null;
                }
            }
        }

        private void KillExistingMediaMtxProcesses()
        {
            try
            {
                var existing = Process.GetProcessesByName("mediamtx");
                foreach (var p in existing)
                {
                    try
                    {
                        _logger.LogWarning("Killing existing mediamtx.exe. PID={Pid}", p.Id);
                        p.Kill(entireProcessTree: true);
                        p.WaitForExit(3000);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to kill existing mediamtx.exe. PID={Pid}", p.Id);
                    }
                    finally
                    {
                        try { p.Dispose(); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enumerate existing mediamtx processes.");
            }
        }
    }
}