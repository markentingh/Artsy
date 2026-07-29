using Artsy.Data.Entities.Projects;

namespace Artsy.Data.Interfaces.Projects
{
    public interface IProjectBlueprintProductImageRepository
    {
        Task<ProjectBlueprintProductImage?> GetByIdAsync(Guid id);
        Task<IEnumerable<ProjectBlueprintProductImage>> GetByProjectBlueprintIdAsync(Guid projectBlueprintId);
        Task<IEnumerable<ProjectBlueprintProductImage>> GetByProjectIdAsync(Guid projectId);
        Task<IEnumerable<ProjectBlueprintProductImage>> GetByBlueprintIdsAsync(IEnumerable<Guid> blueprintIds);
        Task<ProjectBlueprintProductImage> CreateAsync(ProjectBlueprintProductImage image);
        Task UpdateAsync(ProjectBlueprintProductImage image);
        Task DeleteAsync(Guid id);
        Task SetStatusAsync(Guid id, int status);
    }
}
