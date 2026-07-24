using Artsy.Data.Entities.Projects;

namespace Artsy.Data.Interfaces.Projects
{
    public interface IProjectImageGenerationRepository
    {
        Task<ProjectImageGeneration> CreateAsync(ProjectImageGeneration generation);
        Task<ProjectImageGeneration?> GetByIdAsync(Guid id);
        Task<IEnumerable<ProjectImageGeneration>> GetByProjectIdAsync(Guid projectId);
        Task<IEnumerable<ProjectImageGeneration>> GetByCollectionIdAsync(Guid collectionId);
        Task<IEnumerable<ProjectImageGeneration>> GetByItemIdAsync(Guid itemId);
    }
}
