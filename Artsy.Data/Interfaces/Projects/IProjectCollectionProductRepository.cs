using Artsy.Data.Entities.Projects;

namespace Artsy.Data.Interfaces.Projects
{
    public interface IProjectCollectionProductRepository
    {
        Task<ProjectCollectionProduct?> GetByIdAsync(Guid id);
        Task<ProjectCollectionProduct?> GetByCollectionAndBlueprintIdAsync(Guid collectionId, Guid projectBlueprintId);
        Task<ProjectCollectionProduct?> GetByNameAndBlueprintIdAsync(string name, int blueprintId);
        Task<IEnumerable<ProjectCollectionProduct>> GetByCollectionIdAsync(Guid collectionId);
        Task<ProjectCollectionProduct> CreateAsync(ProjectCollectionProduct product);
        Task UpdateAsync(ProjectCollectionProduct product);
        Task BulkUpdateActiveAsync(Guid collectionId, IEnumerable<ProjectCollectionProduct> products);
        Task DeleteAsync(Guid id);
        Task UpdateNameAsync(Guid id, string name);
        Task UpdateActiveAsync(Guid id, bool active);
    }
}
