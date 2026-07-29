using Artsy.Data.Entities;

namespace Artsy.Data.Interfaces
{
    public interface IPrintifyBlueprintImageVariantRepository
    {
        Task<IEnumerable<PrintifyBlueprintImageVariant>> GetByBlueprintImageIdAsync(Guid blueprintImageId);
        Task<IEnumerable<PrintifyBlueprintImageVariant>> GetByBlueprintImageIdsAsync(IEnumerable<Guid> blueprintImageIds);
        Task UpsertAsync(Guid blueprintImageId, string variantColor);
        Task UpsertAsync(Guid blueprintImageId, IEnumerable<string> variantColors);
        Task DeleteByBlueprintImageIdAsync(Guid blueprintImageId);
        Task DeleteByBlueprintImageIdsAsync(IEnumerable<Guid> blueprintImageIds);
        Task DeleteAsync(Guid blueprintImageId, string variantColor);
        Task DeleteAsync(Guid blueprintImageId, IEnumerable<string> variantColors);
    }
}
