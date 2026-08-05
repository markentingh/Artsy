using Artsy.Data.Entities;

namespace Artsy.Data.Interfaces
{
    public interface IAppUserSubscriptionRepository
    {
        Task<AppUserSubscription> CreateAsync(AppUserSubscription subscription);
        Task<AppUserSubscription?> GetByIdAsync(int id);
        Task<IEnumerable<AppUserSubscription>> GetAllAsync();
        Task<IEnumerable<AppUserSubscription>> GetByAppUserIdAsync(Guid appUserId);
        Task<IEnumerable<AppUserSubscription>> GetActiveByAppUserIdAsync(Guid appUserId);
        Task CancelAsync(int id);
    }
}
