using Artsy.Data.Entities.Projects;

namespace Artsy.Data.Interfaces.Projects
{
    public interface IProjectCollectionRepository
    {
        Task<IEnumerable<ProjectCollection>> GetByProjectIdAsync(Guid projectId);
        Task<ProjectCollection?> GetByIdAsync(Guid id);
        Task<ProjectCollection> CreateAsync(ProjectCollection collection);
        Task UpdateAsync(ProjectCollection collection);
        Task UpdateTitleAsync(Guid id, string title);
        Task UpdateDescriptionAsync(Guid id, string? description);
        Task UpdateMultiProductJsonAsync(Guid id, string? multiProductJson);
        Task DeleteAsync(Guid id);
    }
}
