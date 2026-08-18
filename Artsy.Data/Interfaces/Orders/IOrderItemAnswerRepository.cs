using Artsy.Data.Entities.Orders;

namespace Artsy.Data.Interfaces.Orders
{
    public interface IOrderItemAnswerRepository
    {
        Task<IEnumerable<OrderItemAnswer>> GetByOrderItemIdAsync(Guid orderItemId);
        Task UpsertAsync(OrderItemAnswer answer);
    }
}
