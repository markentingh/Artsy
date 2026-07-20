using Artsy.Data.Entities;

namespace Artsy.Data.Interfaces
{
    public interface IPrintifyBlueprintRepository
    {
        Task<int> GetCountAsync();
        Task<int> GetCountAsync(string keyword, string brand);
        Task<IEnumerable<PrintifyBlueprint>> SearchAsync(string keyword, string brand, int start, int length);
        Task<PrintifyBlueprint?> GetByBlueprintIdAsync(int blueprintId);
        Task UpsertAsync(PrintifyBlueprint blueprint);
        Task UpsertBatchAsync(IEnumerable<PrintifyBlueprint> blueprints);
        Task<IEnumerable<string>> GetBrandsAsync();
        Task<IEnumerable<int>> GetAllBlueprintIdsAsync();
        Task DeleteAllAsync();
        Task UpdatePublishedAsync(int blueprintId, bool published);
    }
}
