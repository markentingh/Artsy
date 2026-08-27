using Artsy.Data.Entities.Projects;

namespace Artsy.Data.Interfaces.Projects
{
    public interface IProjectCollectionArtworkReferenceRepository
    {
        Task<IEnumerable<ProjectCollectionArtworkReference>> GetByCollectionAndItemIdAsync(Guid collectionId, Guid itemId);
        Task<IEnumerable<ProjectCollectionArtworkReference>> GetByCollectionIdAsync(Guid collectionId);
        Task<ProjectCollectionArtworkReference?> GetByIdAsync(Guid id);
        Task<ProjectCollectionArtworkReference> CreateAsync(ProjectCollectionArtworkReference reference);
        Task DeleteAsync(Guid id);
    }
}
