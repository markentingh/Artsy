using Artsy.Data.Entities.Projects;

namespace Artsy.Data.Interfaces.Projects
{
    public interface IProjectCollectionArtworkPlacementRepository
    {
        Task<IEnumerable<ProjectCollectionArtworkPlacement>> GetByArtworkIdAsync(Guid collectionArtworkId);
        Task<ProjectCollectionArtworkPlacement?> GetByArtworkIdAndIndexAsync(Guid collectionArtworkId, int index);
        Task<ProjectCollectionArtworkPlacement> CreateAsync(ProjectCollectionArtworkPlacement placement);
        Task UpdateAsync(ProjectCollectionArtworkPlacement placement);
        Task DeleteByArtworkIdAsync(Guid collectionArtworkId);
        Task SetPrintifyImageIdAsync(Guid placementId, string printifyImageId);
        Task SetFullSizeAsync(Guid placementId, bool fullSize);
    }
}
