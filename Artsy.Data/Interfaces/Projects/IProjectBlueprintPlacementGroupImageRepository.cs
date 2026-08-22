using Artsy.Data.Entities.Projects;

namespace Artsy.Data.Interfaces.Projects
{
    public interface IProjectBlueprintPlacementGroupImageRepository
    {
        Task<IEnumerable<ProjectBlueprintPlacementGroupImage>> GetByGroupIdAsync(Guid groupId);
        Task<IEnumerable<ProjectBlueprintPlacementGroupImage>> GetByProjectAndBlueprintAsync(Guid projectId, int blueprintId);
        Task<ProjectBlueprintPlacementGroupImage> CreateAsync(ProjectBlueprintPlacementGroupImage image);
        Task UpdateAsync(ProjectBlueprintPlacementGroupImage image);
        Task DeleteAsync(Guid id);
        Task DeleteByGroupIdAsync(Guid groupId);
    }
}
