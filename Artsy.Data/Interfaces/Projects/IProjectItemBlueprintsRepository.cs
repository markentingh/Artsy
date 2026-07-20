using Artsy.Data.Entities.Projects;

namespace Artsy.Data.Interfaces.Projects
{
    public interface IProjectBlueprintsRepository
    {
        Task<IEnumerable<ProjectBlueprints>> GetByProjectIdAsync(Guid projectId);
        Task<IEnumerable<ProjectBlueprintListDto>> GetListByProjectIdAsync(Guid projectId);
        Task<ProjectBlueprints?> GetByIdAsync(Guid id);
        Task<ProjectBlueprints> CreateAsync(ProjectBlueprints blueprint);
        Task UpdateAsync(ProjectBlueprints blueprint);
        Task UpdatePlacementAsync(Guid id, string placementJson);
        Task DeleteAsync(Guid id);
    }
}
