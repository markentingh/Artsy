using Artsy.Data.Entities.Projects;

namespace Artsy.Data.Interfaces.Projects
{
    public interface IProjectRepository
    {
        Task<IEnumerable<Project>> GetAllAsync(Guid appUserId);
        Task<IEnumerable<Project>> GetArchivedAsync(Guid appUserId);
        Task<Project?> GetByIdAsync(Guid id, Guid appUserId);
        Task<Project?> GetByKeyAsync(string key);
        Task<Project> CreateAsync(Project project);
        Task UpdateAsync(Project project);
        Task UpdatePrintifyStoreIdAsync(Guid id, Guid appUserId, int? printifyStoreId);
        Task UpdateInstagramIdAsync(Guid id, Guid appUserId, Guid? instagramId);
        Task UpdatePostToInstagramAsync(Guid id, Guid appUserId, bool postToInstagram);
        Task DeleteAsync(Guid id, Guid appUserId);
        Task UnarchiveAsync(Guid id, Guid appUserId);
    }
}
