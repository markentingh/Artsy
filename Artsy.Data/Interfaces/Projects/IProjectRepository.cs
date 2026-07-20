using Artsy.Data.Entities.Projects;

namespace Artsy.Data.Interfaces.Projects
{
    public interface IProjectRepository
    {
        Task<IEnumerable<Project>> GetAllAsync(Guid appUserId);
        Task<Project?> GetByIdAsync(Guid id, Guid appUserId);
        Task<Project?> GetByKeyAsync(string key);
        Task<Project> CreateAsync(Project project);
        Task UpdateAsync(Project project);
        Task DeleteAsync(Guid id, Guid appUserId);
    }
}
