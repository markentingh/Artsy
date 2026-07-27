using Artsy.Data.Entities.Projects;

namespace Artsy.Data.Interfaces.Projects
{
    public interface IProjectCollectionProductImageRepository
    {
        Task<ProjectCollectionProductImage?> GetByIdAsync(Guid id);
        Task<IEnumerable<ProjectCollectionProductImage>> FilterByProjectIdsAsync(Guid[] projectIds, int length = 5);
        Task<ProjectCollectionProductImage?> GetByCollectionBlueprintVariantPlacementAsync(Guid collectionId, Guid projectBlueprintId, int variant, int placement);
        Task<IEnumerable<ProjectCollectionProductImage>> GetByCollectionIdAsync(Guid collectionId);
        Task<IEnumerable<ProjectCollectionProductImage>> GetAllByCollectionIdAsync(Guid collectionId);
        Task<IEnumerable<ProjectCollectionProductImage>> GetByCollectionAndBlueprintIdAsync(Guid collectionId, Guid projectBlueprintId);
        Task<ProjectCollectionProductImage> CreateAsync(ProjectCollectionProductImage image);
        Task UpdateAsync(ProjectCollectionProductImage image);
        Task SetInactiveAsync(Guid collectionId, Guid projectBlueprintId, int variant, int placement);
        Task SetPrintifyImageIdAsync(Guid id, string printifyImageId);
        Task DeleteAsync(Guid id);
    }
}
