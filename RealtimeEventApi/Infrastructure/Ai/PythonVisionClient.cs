using System.Text;
using System.Text.Json;
using FactoryApi.Application.Ai;

namespace FactoryApi.Infrastructure.Ai
{
    public sealed class PythonVisionClient
    {
        private readonly HttpClient _httpClient;

        public PythonVisionClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<PythonValidateRoiResponse> ValidateRoiAsync(
    PythonValidateRoiRequest request,
    CancellationToken ct)
        {
            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync("/validate-roi", content, ct);

            var responseText = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                return new PythonValidateRoiResponse
                {
                    Success = false,
                    Message = $"Python API 오류: {(int)response.StatusCode}, {responseText}"
                };
            }

            return JsonSerializer.Deserialize<PythonValidateRoiResponse>(responseText, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new PythonValidateRoiResponse
            {
                Success = false,
                Message = "Python 응답 파싱 실패"
            };
        }
    }
}