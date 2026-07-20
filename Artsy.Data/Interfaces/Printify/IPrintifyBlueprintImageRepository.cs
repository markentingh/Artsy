using Artsy.Data.Entities;

namespace Artsy.Data.Interfaces
{
    public interface IPrintifyBlueprintImageRepository
    {
        Task<IEnumerable<PrintifyBlueprintImage>> GetByBlueprintIdAsync(int blueprintId);
        Task UpsertAsync(PrintifyBlueprintImage image);
    }
}
