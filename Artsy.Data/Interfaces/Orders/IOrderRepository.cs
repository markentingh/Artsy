using Artsy.Data.Entities.Orders;
using Artsy.Data.Models;

namespace Artsy.Data.Interfaces.Orders
{
    public interface IOrderRepository : IDisposable
    {
        Task<IEnumerable<Order>> GetByUserAsync(Guid appUserId);
        Task<IEnumerable<OrderWithDetails>> GetByUserWithDetailsAsync(Guid appUserId);
        Task<Order?> GetByOrderIdAsync(string orderId);
        Task<OrderWithDetails?> GetByIdWithDetailsAsync(Guid id);
        Task<IEnumerable<PrintifyShopWithUser>> GetDistinctActiveShopsAsync();
        Task<SyncResultItem> SyncOrderAsync(Order order, List<OrderItem> items, List<OrderShipment> shipments, string dataHash);
    }

    public class SyncResultItem
    {
        public bool IsNew { get; set; }
        public bool IsUpdated { get; set; }
    }
}
