using Artsy.Data.Entities;

namespace Artsy.Data.Interfaces
{
    public interface IPrintifyBlueprintShippingRepository
    {
        Task<PrintifyBlueprintShipping?> GetByBlueprintAndProviderAsync(int blueprintId, int printProviderId);
        Task UpsertAsync(PrintifyBlueprintShipping shipping);
        Task DeleteByBlueprintAndProviderAsync(int blueprintId, int printProviderId);
    }
}
