using Artsy.Data.Entities.Orders;

namespace Artsy.Data.Interfaces.Orders
{
    public interface IOrderItemArtworkRepository
    {
        Task<IEnumerable<OrderItemArtwork>> GetByOrderItemIdAsync(Guid orderItemId);
        Task<OrderItemArtwork?> GetByIdAsync(Guid id);
        Task<OrderItemArtwork> CreateAsync(OrderItemArtwork artwork);
        Task UpdateAsync(OrderItemArtwork artwork);
        Task DeleteAsync(Guid id);
    }
}
