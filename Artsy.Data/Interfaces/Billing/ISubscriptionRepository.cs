using Artsy.Data.Entities;

namespace Artsy.Data.Interfaces
{
    public interface ISubscriptionRepository
    {
        Task<Subscription> CreateAsync(Subscription subscription);
        Task<Subscription?> GetByIdAsync(int id);
        Task<IEnumerable<Subscription>> GetAllAsync();
        Task<IEnumerable<Subscription>> GetActiveAsync();
        Task UpdateAsync(Subscription subscription);
        Task ArchiveAsync(int id);
        Task ReorderAsync(IEnumerable<int> ids);
        Task SetFeaturedAsync(int id);
    }
}
