using Artsy.Data.Entities;

namespace Artsy.Data.Interfaces
{
    public interface IPrintifyBlueprintRepository
    {
        Task<int> GetCountAsync();
        Task<int> GetCountAsync(string keyword, string brand, bool? published = null);
        Task<IEnumerable<PrintifyBlueprint>> SearchAsync(string keyword, string brand, int start, int length, bool? published = null, string? sort = null);
        Task<PrintifyBlueprint?> GetByBlueprintIdAsync(int blueprintId);
        Task<IEnumerable<PrintifyBlueprint>> GetByBlueprintIdsAsync(IEnumerable<int> blueprintIds);
        Task UpsertAsync(PrintifyBlueprint blueprint);
        Task UpsertBatchAsync(IEnumerable<PrintifyBlueprint> blueprints);
        Task<IEnumerable<string>> GetBrandsAsync();
        Task<IEnumerable<int>> GetAllBlueprintIdsAsync();
        Task<IEnumerable<(int BlueprintId, int ImageCount, int ImagesDownloaded, DateTime DateCreated)>> GetAllBlueprintsImageInfoAsync();
        Task DeleteAllAsync();
        Task UpdatePublishedAsync(int blueprintId, bool published);
        Task UpdateImagePromptAsync(int blueprintId, string imagePrompt);
        Task UpdateImagesDownloadedAsync(int blueprintId, int imagesDownloaded);
        Task UpdateStatusAsync(int blueprintId, int status);
        Task SetMissingStatusAsync(IEnumerable<int> missingBlueprintIds, int status);
    }
}
