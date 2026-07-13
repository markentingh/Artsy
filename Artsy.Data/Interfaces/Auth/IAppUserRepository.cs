using Artsy.Data.Entities.Auth;
using Artsy.Data.Models;

namespace Artsy.Data.Interfaces.Auth
{
    public interface IAppUserRepository : IDisposable
    {
        int GetTotalUsers();
        Task<IEnumerable<AppUser>> GetAll();
        Task<(IList<FilteredUserResult> items, int totalCount)> GetAllFiltered(string fullName, int role, string sort, int page = 1, int pageSize = 10);
        Task<AppUser> FindByGuidAsync(Guid userId, bool activeOnly = false);
        Task<AppUser> FindByUserEmailAsync(string emailAddress, bool activeOnly = true);
        Task<AppUser> Add(AppUser user);
        void UpdateInfo(AppUser user);
        Task ActivateAccount(AppUser user);
        Task UpdateFailedCount(AppUser user);
        Task DeleteUserAsync(Guid userId);
        Task UpdatePassword(AppUser user);
        Task<AppUser> UpdatePasswordResetHash(AppUser user);
        Task<AppUser> FindByPasswordResetHashAsync(string hashPassword, bool activeOnly = true);
        Task<AppUser> UpdatePasswordHash(AppUser user);
        Task<bool> UpdateLock(Guid userId, bool lockUser);
        void UpdateOAuthState(AppUser user);
        void UpdatePrintifyTokens(AppUser user);
        void UpdateMetaTokens(AppUser user);
        void UpdateTelegramConnection(AppUser user);
        Task<AppUser?> FindByOAuthStateAsync(string state);
        Task<AppUser?> FindByOAuthStatePrefixAsync(string prefix);
        Task<AppUser?> FindByTelegramConnectionTokenAsync(string token);
        Task<AppUser?> FindByTelegramUserIdAsync(string telegramUserId);
    }
}
