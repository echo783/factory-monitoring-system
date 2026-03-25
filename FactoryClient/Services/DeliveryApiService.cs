using FactoryClient.Models;
using System.Net.Http;
using System.Net.Http.Json;

namespace FactoryClient.Services
{
    public class DeliveryApiService
    {
        private readonly HttpClient _httpClient;

        public DeliveryApiService()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://localhost:7125/")
            };
        }

        public async Task<List<DeliveryListDto>> GetDeliveryListAsync()
        {
            var result = await _httpClient.GetFromJsonAsync<List<DeliveryListDto>>("api/delivery");
            return result ?? new List<DeliveryListDto>();
        }

        public async Task<(bool Success, string Message)> CreateDeliveryAsync(DeliveryCreateRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync("api/delivery", request);

            var responseText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return (true, "저장되었습니다.");
            }

            if (string.IsNullOrWhiteSpace(responseText))
            {
                return (false, "저장 실패");
            }

            return (false, responseText);
        }

        public async Task<List<string>> GetProductListAsync()
        {
            var result = await _httpClient.GetFromJsonAsync<List<string>>("api/inventory/products");
            return result ?? new List<string>();
        }

        public async Task<ProductStockDto?> GetProductStockAsync(string productName)
        {
            if (string.IsNullOrWhiteSpace(productName))
                return null;

            var url = $"api/inventory/stock?productName={Uri.EscapeDataString(productName)}";
            var result = await _httpClient.GetFromJsonAsync<ProductStockDto>(url);
            return result;
        }


    }
}