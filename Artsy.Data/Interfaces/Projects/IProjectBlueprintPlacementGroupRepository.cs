using Artsy.Data.Entities.Projects;

namespace Artsy.Data.Interfaces.Projects
{
    public interface IProjectBlueprintPlacementGroupRepository
    {
        Task<IEnumerable<ProjectBlueprintPlacementGroup>> GetByProjectAndBlueprintAsync(Guid projectId, int blueprintId);
        Task<ProjectBlueprintPlacementGroup> CreateAsync(ProjectBlueprintPlacementGroup group);
        Task DeleteAsync(Guid id);
    }
}
