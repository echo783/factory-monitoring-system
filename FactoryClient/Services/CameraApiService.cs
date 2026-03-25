using FactoryClient.Models;
using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Net.Http.Json;

namespace FactoryClient.Services
{
    public class CameraApiService
    {
        private readonly HttpClient _http;

        public CameraApiService()
        {
            _http = new HttpClient(new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true
            });

            _http.BaseAddress = new Uri("https://localhost:7125/");
        }

        public async Task<DebugStateDto?> GetDebugStateAsync(int cameraId)
        {
            var res = await _http.GetAsync($"api/Camera/{cameraId}/debug-state");

            if (!res.IsSuccessStatusCode)
                return null;

            var json = await res.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<DebugStateDto>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }

        public async Task<BitmapImage?> GetCameraImageAsync(int cameraId)
        {
            try
            {
                var stream = await _http.GetStreamAsync($"api/Camera/{cameraId}/image?t={DateTime.Now.Ticks}");

                using var memory = new MemoryStream();
                await stream.CopyToAsync(memory);
                memory.Position = 0;

                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = memory;
                image.EndInit();
                image.Freeze();

                return image;
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<ProductionEventDto>> GetProductionEventsAsync(
            int? cameraId,
            DateTime? from,
            DateTime? to)
        {
            var query = new List<string>();

            if (cameraId.HasValue)
                query.Add($"cameraId={cameraId.Value}");

            if (from.HasValue)
                query.Add($"from={Uri.EscapeDataString(from.Value.ToString("yyyy-MM-dd HH:mm:ss"))}");

            if (to.HasValue)
                query.Add($"to={Uri.EscapeDataString(to.Value.ToString("yyyy-MM-dd HH:mm:ss"))}");

            var url = "api/history/events";
            if (query.Count > 0)
                url += "?" + string.Join("&", query);

            var result = await _http.GetFromJsonAsync<List<ProductionEventDto>>(url);
            return result ?? new List<ProductionEventDto>();
        }

        public async Task<CameraControlStatusDto?> GetCameraRunStatusAsync(int cameraId)
        {
            var res = await _http.GetAsync($"api/Camera/{cameraId}/status");

            if (!res.IsSuccessStatusCode)
                return null;

            var json = await res.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<CameraControlStatusDto>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }

        public async Task<bool> StartCameraAsync(int cameraId)
        {
            var res = await _http.PostAsync($"api/Camera/{cameraId}/start", null);
            return res.IsSuccessStatusCode;
        }

        public async Task<bool> StopCameraAsync(int cameraId)
        {
            var res = await _http.PostAsync($"api/Camera/{cameraId}/stop", null);
            return res.IsSuccessStatusCode;
        }
    }
}