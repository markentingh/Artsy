using Artsy.Data.Entities.Projects;

namespace Artsy.Data.Interfaces.Projects
{
    public interface IProjectCollectionAnswerRepository
    {
        Task<IEnumerable<ProjectCollectionAnswer>> GetByCollectionIdAsync(Guid collectionId);
        Task<ProjectCollectionAnswer> CreateAsync(ProjectCollectionAnswer answer);
        Task UpsertAsync(ProjectCollectionAnswer answer);
    }
}
