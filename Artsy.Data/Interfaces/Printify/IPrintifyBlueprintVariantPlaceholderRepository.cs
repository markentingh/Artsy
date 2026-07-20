using Artsy.Data.Entities;

namespace Artsy.Data.Interfaces
{
    public interface IPrintifyBlueprintVariantPlaceholderRepository
    {
        Task<IEnumerable<PrintifyBlueprintVariantPlaceholder>> GetByVariantIdAsync(int variantId);
        Task UpsertBatchAsync(IEnumerable<PrintifyBlueprintVariantPlaceholder> placeholders);
        Task DeleteByVariantIdsAsync(IEnumerable<int> variantIds);
    }
}
