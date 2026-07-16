using Artsy.Data.Entities.Projects;

namespace Artsy.Data.Interfaces.Projects
{
    public interface IProjectQuestionRepository
    {
        Task<IEnumerable<ProjectQuestion>> GetByProjectIdAsync(Guid projectId);
        Task<ProjectQuestion?> GetByIdAsync(Guid id);
        Task<ProjectQuestion> CreateAsync(ProjectQuestion question);
        Task UpdateAsync(ProjectQuestion question);
        Task DeleteAsync(Guid id);
    }
}
