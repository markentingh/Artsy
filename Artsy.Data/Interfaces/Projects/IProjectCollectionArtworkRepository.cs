using Artsy.Data.Entities.Projects;

namespace Artsy.Data.Interfaces.Projects
{
    public interface IProjectCollectionArtworkRepository
    {
        Task<IEnumerable<ProjectCollectionArtwork>> FilterByProjectIdAsync(Guid projectId, Guid? collectionId = null, int start = 0, int length = 5);
        Task<IEnumerable<ProjectCollectionArtwork>> FilterByProjectIdsAsync(Guid[] projectIds, int length = 5);
        Task<IEnumerable<ProjectCollectionArtwork>> GetByCollectionIdAsync(Guid collectionId);
        Task<ProjectCollectionArtwork?> GetByIdAsync(Guid collectionId, Guid artworkId);
        Task<ProjectCollectionArtwork?> GetByCollectionAndItemIdAsync(Guid collectionId, Guid itemId);
        Task<ProjectCollectionArtwork> CreateAsync(ProjectCollectionArtwork artwork);
        Task<ProjectCollectionArtwork> UpsertAsync(ProjectCollectionArtwork artwork);
        Task UpdateAsync(ProjectCollectionArtwork artwork);
        Task AcceptAsync(Guid collectionId, Guid itemId);
    }
}
