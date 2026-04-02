namespace FactoryApi.Infrastructure.MediaMtx
{
    public class MediaMtxOptions
    {
        public bool Enabled { get; set; } = true;
        public string ExeRelativePath { get; set; } = "tools/mediamtx/mediamtx.exe";
        public string ConfigRelativePath { get; set; } = "tools/mediamtx/mediamtx.yml";
        public bool AutoRestart { get; set; } = true;
        public int RestartDelayMs { get; set; } = 2000;
    }
}