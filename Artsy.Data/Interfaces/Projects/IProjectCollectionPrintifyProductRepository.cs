using Artsy.Data.Entities.Projects;

namespace Artsy.Data.Interfaces.Projects
{
    public interface IProjectCollectionPrintifyProductRepository
    {
        Task<ProjectCollectionPrintifyProduct?> GetByIdAsync(Guid id);
        Task<ProjectCollectionPrintifyProduct?> GetByProductIdAsync(Guid productId);
        Task<ProjectCollectionPrintifyProduct?> GetByPrintifyProductIdAsync(string printifyProductId);
        Task<ProjectCollectionPrintifyProduct?> GetByCollectionAndProductIdAsync(Guid collectionId, Guid productId);
        Task<IEnumerable<ProjectCollectionPrintifyProduct>> GetByCollectionIdAsync(Guid collectionId);
        Task<ProjectCollectionPrintifyProduct> CreateAsync(ProjectCollectionPrintifyProduct product);
        Task UpdateAsync(ProjectCollectionPrintifyProduct product);
        Task SetPublishedAsync(Guid id, bool published);
        Task DeleteAsync(Guid id);
    }
}
