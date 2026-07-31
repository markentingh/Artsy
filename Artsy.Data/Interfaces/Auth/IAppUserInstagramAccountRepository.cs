using Artsy.Data.Entities.Auth;

namespace Artsy.Data.Interfaces.Auth
{
    public interface IAppUserInstagramAccountRepository
    {
        Task<IEnumerable<AppUserInstagramAccount>> GetByUserIdAsync(Guid appUserId);
        Task<AppUserInstagramAccount?> GetByIdAsync(Guid id);
        Task<AppUserInstagramAccount?> GetByInstagramBusinessAccountIdAsync(Guid appUserId, string instagramBusinessAccountId);
        Task<AppUserInstagramAccount> UpsertAsync(AppUserInstagramAccount account);
        Task DeleteAsync(Guid id, Guid appUserId);
    }
}
