using Artsy.Data.Entities.Orders;

namespace Artsy.Data.Interfaces.Orders
{
    public interface IOrderItemArtworkRepository
    {
        Task<IEnumerable<OrderItemArtwork>> GetByOrderItemIdAsync(Guid orderItemId);
        Task<OrderItemArtwork?> GetByIdAsync(Guid id);
        Task<OrderItemArtwork> CreateAsync(OrderItemArtwork artwork);
        Task UpdateAsync(OrderItemArtwork artwork);
        Task UpdateActiveAsync(Guid id, bool active, DateTime updated);
        Task UpdateOpacityAsync(Guid id, bool opacity);
        Task UpdateAcceptedAsync(Guid id, bool accepted, DateTime updated);
        Task DeleteAsync(Guid id);
    }
}
