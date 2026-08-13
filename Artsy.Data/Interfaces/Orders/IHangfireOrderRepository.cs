using Artsy.Data.Entities.Orders;

namespace Artsy.Data.Interfaces.Orders
{
    public interface IHangfireOrderRepository : IDisposable
    {
        Task<HangfireOrder?> GetLatestAsync();
        Task<IEnumerable<HangfireOrder>> GetByDateRangeAsync(DateTime since);
        Task AddAsync(HangfireOrder record);
    }
}
