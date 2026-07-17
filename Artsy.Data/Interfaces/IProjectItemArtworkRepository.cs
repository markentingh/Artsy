using Artsy.Data.Entities.Projects;

namespace Artsy.Data.Interfaces.Projects
{
    public interface IProjectItemArtworkRepository
    {
        Task<IEnumerable<ProjectItemArtwork>> GetByProjectIdAsync(Guid projectId);
        Task<IEnumerable<ProjectItemArtwork>> GetByItemIdAsync(Guid itemId);
        Task<ProjectItemArtwork?> GetByIdAsync(Guid id);
        Task<ProjectItemArtwork> CreateAsync(ProjectItemArtwork artwork);
        Task UpdateAsync(ProjectItemArtwork artwork);
        Task DeleteAsync(Guid id);
    }
}
