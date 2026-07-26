using Artsy.Data.Entities;

namespace Artsy.Data.Interfaces
{
    public interface IPrintifyBlueprintImageVariantRepository
    {
        Task<IEnumerable<PrintifyBlueprintImageVariant>> GetByImageIdAsync(Guid imageId);
        Task<IEnumerable<PrintifyBlueprintImageVariant>> GetByImageIdsAsync(IEnumerable<Guid> imageIds);
        Task<IEnumerable<PrintifyBlueprintImageVariant>> GetByBlueprintIdsAsync(IEnumerable<int> blueprintIds);
        Task DeleteByImageIdAsync(Guid imageId);
        Task DeleteByImageAndVariantIdsAsync(Guid imageId, IEnumerable<int> variantIds);
        Task InsertBatchAsync(IEnumerable<PrintifyBlueprintImageVariant> imageVariants);
    }
}
