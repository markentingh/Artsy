using Artsy.Data.Entities.Projects;

namespace Artsy.Data.Interfaces.Projects
{
    public interface IProjectItemBlueprintRepository
    {
        Task<IEnumerable<ProjectItemBlueprint>> GetByItemIdAsync(Guid itemId);
        Task<IEnumerable<ProjectItemBlueprint>> GetByItemIdsAsync(IEnumerable<Guid> itemIds);
        Task<ProjectItemBlueprint?> GetByIdAsync(Guid id);
        Task<ProjectItemBlueprint> CreateAsync(ProjectItemBlueprint blueprint);
        Task UpdateAsync(ProjectItemBlueprint blueprint);
        Task DeleteAsync(Guid id);
    }
}
