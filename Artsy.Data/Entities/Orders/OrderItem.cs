namespace Artsy.Data.Entities.Orders
{
    public class OrderItem
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public string ProductId { get; set; } = "";
        public int Quantity { get; set; }
        public int VariantId { get; set; }
        public int PrintProviderId { get; set; }
        public int Cost { get; set; }
        public int ShippingCost { get; set; }
        public string Status { get; set; } = "";
        public string Metadata { get; set; } = "";
        public DateTime? DateSentToProduction { get; set; }
        public DateTime? DateFulfilled { get; set; }
    }
}
