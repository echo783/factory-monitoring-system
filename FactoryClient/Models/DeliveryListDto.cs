namespace FactoryClient.Models
{
    public class DeliveryListDto
    {
        public long DeliveryId { get; set; }
        public DateTime DeliveryDate { get; set; }

        public string CustomerName { get; set; } = "";
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }

        public string ProductName { get; set; } = "";
        public int Quantity { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}