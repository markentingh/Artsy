using Artsy.Data.Entities.Projects;

namespace Artsy.Data.Interfaces.Projects
{
    public interface IProjectCollectionArtworkPlacementRepository
    {
        Task<IEnumerable<ProjectCollectionArtworkPlacement>> GetByArtworkIdAsync(Guid collectionArtworkId);
        Task<ProjectCollectionArtworkPlacement?> GetByArtworkIdAndIndexAsync(Guid collectionArtworkId, int index);
        Task<ProjectCollectionArtworkPlacement?> GetByArtworkIdGroupAndPositionAsync(Guid collectionArtworkId, Guid groupId, string position);
        Task<ProjectCollectionArtworkPlacement> CreateAsync(ProjectCollectionArtworkPlacement placement);
        Task UpdateAsync(ProjectCollectionArtworkPlacement placement);
        Task DeleteByArtworkIdAsync(Guid collectionArtworkId);
        Task SetPrintifyImageIdAsync(Guid placementId, string printifyImageId);
        Task SetFullSizeAsync(Guid placementId, bool fullSize);
        Task SetOptionalPromptAsync(Guid placementId, string optionalPrompt);
        Task SetOptionalPromptByGroupAsync(Guid collectionArtworkId, Guid groupId, string optionalPrompt);
    }
}
