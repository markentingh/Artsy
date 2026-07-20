using Artsy.Data.Entities.Projects;

namespace Artsy.Data.Interfaces.Projects
{
    public interface IProjectItemQuestionRepository
    {
        Task<IEnumerable<ProjectItemQuestion>> GetByItemIdAsync(Guid itemId);
        Task<IEnumerable<ProjectItemQuestion>> GetByProjectIdAsync(Guid projectId);
        Task<ProjectItemQuestion?> GetByIdAsync(Guid id);
        Task<ProjectItemQuestion> CreateAsync(ProjectItemQuestion question);
        Task UpdateAsync(ProjectItemQuestion question);
        Task DeleteAsync(Guid id);
    }
}
