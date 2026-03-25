using System.Text.Json.Serialization;

namespace FactoryClient.Models
{
    public class ProductStockDto
    {
        [JsonPropertyName("productName")]
        public string ProductName { get; set; } = "";

        [JsonPropertyName("remainQuantity")]
        public int RemainQuantity { get; set; }
    }
}