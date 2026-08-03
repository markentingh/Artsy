using Artsy.Data.Entities.Projects;

namespace Artsy.Data.Interfaces.Projects
{
    public interface ICustomImageRepository
    {
        Task<IEnumerable<CustomImage>> GetByUserIdAsync(Guid appUserId, int limit = 10, int offset = 0);
        Task<CustomImage?> GetByIdAsync(Guid id);
        Task<CustomImage> CreateAsync(CustomImage image);
        Task DeleteAsync(Guid id);
        Task<int> CountByUserIdAsync(Guid appUserId);
    }
}
