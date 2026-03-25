namespace FactoryClient.Models
{
    public class DeliveryCreateRequest
    {
        public string CustomerName { get; set; } = "";
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public DateTime DeliveryDate { get; set; }

        public string ProductName { get; set; } = "";
        public int Quantity { get; set; }

        public string? Memo { get; set; }
    }
}