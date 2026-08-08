using Artsy.Data.Entities.Projects;

namespace Artsy.Data.Interfaces.Projects
{
    public interface IProjectIdeaRepository
    {
        Task<IEnumerable<ProjectIdea>> GetByProjectIdAsync(Guid projectId);
        Task<ProjectIdea?> GetByIdAsync(Guid id);
        Task<ProjectIdea> CreateAsync(ProjectIdea idea);
        Task DeleteAsync(Guid id);
    }
}
