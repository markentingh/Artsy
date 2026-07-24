using Artsy.Data.Entities.Projects;

namespace Artsy.Data.Interfaces.Projects
{
    public interface IProjectCollectionProductImageRepository
    {
        Task<ProjectCollectionProductImage?> GetByIdAsync(Guid id);
        Task<ProjectCollectionProductImage?> GetByCollectionBlueprintVariantPlacementAsync(Guid collectionId, Guid projectBlueprintId, int variant, int placement);
        Task<IEnumerable<ProjectCollectionProductImage>> GetByCollectionIdAsync(Guid collectionId);
        Task<IEnumerable<ProjectCollectionProductImage>> GetByCollectionAndBlueprintIdAsync(Guid collectionId, Guid projectBlueprintId);
        Task<ProjectCollectionProductImage> CreateAsync(ProjectCollectionProductImage image);
        Task UpdateAsync(ProjectCollectionProductImage image);
        Task DeleteAsync(Guid id);
    }
}
