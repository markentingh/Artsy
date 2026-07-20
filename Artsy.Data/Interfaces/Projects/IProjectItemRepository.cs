using Artsy.Data.Entities.Projects;

namespace Artsy.Data.Interfaces.Projects
{
    public interface IProjectItemRepository
    {
        Task<IEnumerable<ProjectItem>> GetByProjectIdAsync(Guid projectId);
        Task<IEnumerable<ProjectItemListDto>> GetListByProjectIdAsync(Guid projectId);
        Task<ProjectItem?> GetByIdAsync(Guid id);
        Task<ProjectItem> CreateAsync(ProjectItem item);
        Task UpdateAsync(ProjectItem item);
        Task ReorderAsync(IEnumerable<Guid> itemIds);
        Task DeleteAsync(Guid id);
    }
}
