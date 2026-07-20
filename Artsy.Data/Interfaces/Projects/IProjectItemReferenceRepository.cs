using Artsy.Data.Entities.Projects;

namespace Artsy.Data.Interfaces.Projects
{
    public interface IProjectItemReferenceRepository
    {
        Task<IEnumerable<ProjectItemReference>> GetByItemIdAsync(Guid itemId);
        Task<IEnumerable<ProjectItemReference>> GetByProjectIdAsync(Guid projectId);
        Task<IEnumerable<ProjectItemThumbnailDto>> GetThumbnailsByProjectIdAsync(Guid projectId);
        Task<ProjectItemReference?> GetByIdAsync(Guid id);
        Task<ProjectItemReference> CreateAsync(ProjectItemReference reference);
        Task DeleteAsync(Guid id);
    }
}
