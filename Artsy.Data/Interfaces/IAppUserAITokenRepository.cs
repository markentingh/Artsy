using Artsy.Data.Entities;

namespace Artsy.Data.Interfaces
{
    public interface IAppUserAITokenRepository
    {
        Task<AppUserAIToken> CreateAsync(AppUserAIToken token);
        Task<AppUserAIToken?> GetByIdAsync(int id);
        Task<IEnumerable<AppUserAIToken>> GetByAppUserIdAsync(Guid appUserId);
        Task<IEnumerable<AppUserAIToken>> GetByAppUserAndMonthAsync(Guid appUserId, DateTime billingMonth);
        Task UpdateTokensUsedAsync(int id, int tokensUsed);
    }
}
