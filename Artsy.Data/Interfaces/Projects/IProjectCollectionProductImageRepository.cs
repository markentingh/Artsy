using Artsy.Data.Entities.Projects;

namespace Artsy.Data.Interfaces.Projects
{
    public interface IProjectCollectionProductImageRepository
    {
        Task<ProjectCollectionProductImage?> GetByIdAsync(Guid id);
        Task<IEnumerable<ProjectCollectionProductImage>> FilterByProjectIdsAsync(Guid[] projectIds, int length = 5);
        Task<ProjectCollectionProductImage?> GetByCollectionBlueprintProductImageIdAsync(Guid collectionId, Guid projectBlueprintId, Guid productImageId, bool activeOnly = true);
        Task<IEnumerable<ProjectCollectionProductImage>> GetByCollectionIdAsync(Guid collectionId);
        Task<IEnumerable<ProjectCollectionProductImage>> GetAllByCollectionIdAsync(Guid collectionId);
        Task<IEnumerable<ProjectCollectionProductImage>> GetByCollectionAndBlueprintIdAsync(Guid collectionId, Guid projectBlueprintId);
        Task<IEnumerable<ProjectCollectionProductImage>> GetByPrintifyProductIdAsync(string printifyProductId);
        Task<ProjectCollectionProductImage> CreateAsync(ProjectCollectionProductImage image);
        Task UpdateAsync(ProjectCollectionProductImage image);
        Task SetInactiveAsync(Guid collectionId, Guid projectBlueprintId, Guid productImageId);
        Task DeleteByCollectionAndBlueprintIdAsync(Guid collectionId, Guid projectBlueprintId);
        Task DeleteAsync(Guid id);
        Task UpdateActiveAsync(Guid id, bool active);
        Task UpdateAcceptedAsync(Guid id, bool accepted);
    }
}
