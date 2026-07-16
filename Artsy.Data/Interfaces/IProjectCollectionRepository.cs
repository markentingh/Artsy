using Artsy.Data.Entities.Projects;

namespace Artsy.Data.Interfaces.Projects
{
    public interface IProjectCollectionRepository
    {
        Task<IEnumerable<ProjectCollection>> GetByProjectIdAsync(Guid projectId);
        Task<ProjectCollection?> GetByIdAsync(Guid id);
        Task<ProjectCollection> CreateAsync(ProjectCollection collection);
        Task UpdateAsync(ProjectCollection collection);
        Task DeleteAsync(Guid id);
    }
}
