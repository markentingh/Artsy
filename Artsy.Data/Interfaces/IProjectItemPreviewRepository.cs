using Artsy.Data.Entities.Projects;

namespace Artsy.Data.Interfaces.Projects
{
    public interface IProjectItemPreviewRepository
    {
        Task<IEnumerable<ProjectItemPreview>> GetByItemIdAsync(Guid itemId);
        Task<ProjectItemPreview?> GetByIdAsync(Guid id);
        Task<ProjectItemPreview> CreateAsync(ProjectItemPreview preview);
        Task DeleteAsync(Guid id);
    }
}
