using Artsy.Data.Entities;

namespace Artsy.Data.Interfaces
{
    public interface IPrintifyBlueprintVariantRepository
    {
        Task<IEnumerable<PrintifyBlueprintVariant>> GetByBlueprintAndProviderAsync(int blueprintId, int printProviderId);
        Task<IEnumerable<PrintifyBlueprintVariant>> GetByBlueprintIdAsync(int blueprintId);
        Task<IEnumerable<PrintifyBlueprintVariant>> GetByBlueprintIdsAsync(IEnumerable<int> blueprintIds);
        Task UpsertBatchAsync(IEnumerable<PrintifyBlueprintVariant> variants);
        Task UpdateHexColorsAsync(int blueprintId, int printProviderId, IEnumerable<(string Color, string HexColor)> colorHexValues);
        Task DeleteByBlueprintAndProviderAsync(int blueprintId, int printProviderId);
        Task<int> ConvertVariantsAsync();
        Task<int> LoadVariantOptionsAsync();
        Task<IEnumerable<(int BlueprintId, int PrintProviderId)>> GetDistinctBlueprintProvidersWithEmptyColorOrSizeAsync();
        Task<(IEnumerable<(string Key, int MaxCount)> Keys, int MaxKeys)> GetDistinctOptionKeysAsync();
    }
}
