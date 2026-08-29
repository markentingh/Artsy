using Artsy.Data.Entities.Projects;

namespace Artsy.Data.Interfaces.Projects
{
    public interface IProjectIdeaRepository
    {
        Task<IEnumerable<ProjectIdea>> GetByProjectIdAsync(Guid projectId);
        Task<ProjectIdea?> GetByIdAsync(Guid id);
        Task<ProjectIdea> CreateAsync(ProjectIdea idea);
        Task UpdateAsync(ProjectIdea idea);
        Task UpdateMetadataJsonAsync(Guid id, string metadataJson);
        Task DeleteAsync(Guid id);
    }
}
