using Artsy.Data.Entities.Projects;

namespace Artsy.Data.Interfaces.Projects
{
    public interface IProjectCollectionPrintifyProductMockupRepository
    {
        Task<IEnumerable<ProjectCollectionPrintifyProductMockup>> GetByPrintifyProductIdAsync(Guid printifyProductId);
        Task<IEnumerable<ProjectCollectionPrintifyProductMockup>> GetByCollectionIdAsync(Guid collectionId);
        Task<ProjectCollectionPrintifyProductMockup> CreateAsync(ProjectCollectionPrintifyProductMockup mockup);
        Task DeleteByPrintifyProductIdAsync(Guid printifyProductId);
    }
}
