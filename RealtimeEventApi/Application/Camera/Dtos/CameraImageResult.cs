namespace RealtimeEventApi.Application.Camera.Dtos
{
    public class CameraImageResult
    {
        public bool CameraExists {  get; set; }
        public bool ImageExists { get; set; }
        public byte[]? Bytes { get; set; }

    }
}
