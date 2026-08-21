using Artsy.Data.Entities.Projects;

namespace Artsy.Data.Interfaces.Projects
{
    public interface IProjectCollectionProductPlacementRepository
    {
        Task<IEnumerable<ProjectCollectionProductPlacement>> GetByProductIdAsync(Guid productId);
        Task<IEnumerable<ProjectCollectionProductPlacement>> GetByProductIdAndVariantIdAsync(Guid productId, int variantId);
        Task DeleteByProductIdAsync(Guid productId);
        Task CreateAsync(ProjectCollectionProductPlacement placement);
    }
}
