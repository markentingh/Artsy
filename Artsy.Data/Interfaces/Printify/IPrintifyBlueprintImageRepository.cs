using Artsy.Data.Entities;

namespace Artsy.Data.Interfaces
{
    public interface IPrintifyBlueprintImageRepository
    {
        Task<IEnumerable<PrintifyBlueprintImage>> GetByBlueprintIdAsync(int blueprintId);
        Task<IEnumerable<PrintifyBlueprintImage>> GetByBlueprintIdsAsync(IEnumerable<int> blueprintIds);
        Task<Guid> UpsertAsync(PrintifyBlueprintImage image);
        Task DeleteByBlueprintIdAsync(int blueprintId);
    }
}
