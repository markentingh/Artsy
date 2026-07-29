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
        Task UpdateVariantsAsync(Guid id, string blueprintJson, int printProviderId);
        Task UpdatePricingAsync(Guid id, string pricingJson);
        Task UpdateDetailsAsync(Guid id, string name, string description, string prompt, string safetyInfo);
        Task DeleteAsync(Guid id);
    }
}
