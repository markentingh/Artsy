using Artsy.Data.Entities.Orders;

namespace Artsy.Data.Models
{
    public class PrintifyShopWithUser
    {
        public Guid AppUserId { get; set; }
        public int PrintifyShopId { get; set; }
    }

    public class OrderWithDetails
    {
        public Order Order { get; set; } = new Order();
        public List<OrderItem> Items { get; set; } = new List<OrderItem>();
        public List<OrderShipment> Shipments { get; set; } = new List<OrderShipment>();
    }
}
