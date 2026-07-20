using Artsy.Data.Entities.Projects;

namespace Artsy.Data.Interfaces.Projects
{
    public interface IProjectCollectionArtworkRepository
    {
        Task<IEnumerable<ProjectCollectionArtwork>> FilterByProjectIdAsync(Guid projectId, Guid? collectionId = null, int start = 0, int length = 5);
        Task<IEnumerable<ProjectCollectionArtwork>> FilterByProjectIdsAsync(Guid[] projectIds, int length = 5);
        Task<ProjectCollectionArtwork?> GetByIdAsync(Guid collectionId, Guid artworkId);
    }
}
