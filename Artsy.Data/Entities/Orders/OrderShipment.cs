namespace Artsy.Data.Entities.Orders
{
    public class OrderShipment
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public string Carrier { get; set; } = "";
        public string Number { get; set; } = "";
        public string Url { get; set; } = "";
        public DateTime? DeliveredAt { get; set; }
    }
}
