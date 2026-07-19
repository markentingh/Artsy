using Artsy.Data.Entities;

namespace Artsy.Data.Interfaces
{
    public interface IPrintifyBlueprintVariantRepository
    {
        Task<IEnumerable<PrintifyBlueprintVariant>> GetByBlueprintAndProviderAsync(int blueprintId, int printProviderId);
        Task UpsertBatchAsync(IEnumerable<PrintifyBlueprintVariant> variants);
        Task DeleteByBlueprintAndProviderAsync(int blueprintId, int printProviderId);
    }
}
