using Artsy.Data.Entities;

namespace Artsy.Data.Interfaces
{
    public interface ITrendRepository
    {
        Task<IEnumerable<Trend>> GetRecentAsync(int limit = 20);
        Task<Trend> CreateAsync(Trend trend);
        Task DeleteAsync(Guid id);
    }
}
