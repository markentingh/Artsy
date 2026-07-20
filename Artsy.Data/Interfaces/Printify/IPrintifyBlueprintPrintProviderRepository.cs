using Artsy.Data.Entities;

namespace Artsy.Data.Interfaces
{
    public interface IPrintifyBlueprintPrintProviderRepository
    {
        Task<IEnumerable<PrintifyBlueprintPrintProvider>> GetByBlueprintIdAsync(int blueprintId);
        Task UpsertBatchAsync(IEnumerable<PrintifyBlueprintPrintProvider> providers);
        Task DeleteByBlueprintIdAsync(int blueprintId);
    }
}
