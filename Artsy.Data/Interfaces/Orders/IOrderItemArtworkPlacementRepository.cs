using Artsy.Data.Entities.Orders;

namespace Artsy.Data.Interfaces.Orders
{
    public interface IOrderItemArtworkPlacementRepository
    {
        Task<IEnumerable<OrderItemArtworkPlacement>> GetByArtworkIdAsync(Guid orderItemArtworkId);
        Task<OrderItemArtworkPlacement?> GetByArtworkIdAndIndexAsync(Guid orderItemArtworkId, int index);
        Task<OrderItemArtworkPlacement> CreateAsync(OrderItemArtworkPlacement placement);
        Task DeleteByArtworkIdAsync(Guid orderItemArtworkId);
    }
}
