using Artsy.Data.Entities;

namespace Artsy.Data.Interfaces
{
    public interface IPrintifyBlueprintVariantRepository
    {
        Task<IEnumerable<PrintifyBlueprintVariant>> GetByBlueprintAndProviderAsync(int blueprintId, int printProviderId);
        Task<IEnumerable<PrintifyBlueprintVariant>> GetByBlueprintIdAsync(int blueprintId);
        Task<IEnumerable<PrintifyBlueprintVariant>> GetByBlueprintIdsAsync(IEnumerable<int> blueprintIds);
        Task UpsertBatchAsync(IEnumerable<PrintifyBlueprintVariant> variants);
        Task DeleteByBlueprintAndProviderAsync(int blueprintId, int printProviderId);
    }
}
