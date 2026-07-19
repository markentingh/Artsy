using Dapper;
using System.Data;
using Artsy.Data.Entities;
using Artsy.Data.Interfaces;

namespace Artsy.Data.Repositories
{
    public class PrintifyBlueprintVariantPlaceholderRepository : IPrintifyBlueprintVariantPlaceholderRepository
    {
        readonly IDbConnection _dbConnection;

        public PrintifyBlueprintVariantPlaceholderRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<PrintifyBlueprintVariantPlaceholder>> GetByVariantIdAsync(int variantId)
        {
            const string query = @"SELECT * FROM public.""PrintifyBlueprintVariantPlaceholders"" WHERE ""VariantId"" = @variantId ORDER BY ""Position""";
            return await _dbConnection.QueryAsync<PrintifyBlueprintVariantPlaceholder>(query, new { variantId });
        }

        public async Task UpsertBatchAsync(IEnumerable<PrintifyBlueprintVariantPlaceholder> placeholders)
        {
            const string query = @"
                INSERT INTO public.""PrintifyBlueprintVariantPlaceholders"" (""VariantId"", ""Position"", ""DecorationMethod"", ""Height"", ""Width"")
                VALUES (@VariantId, @Position, @DecorationMethod, @Height, @Width)
                ON CONFLICT (""VariantId"", ""Position"")
                DO UPDATE SET
                    ""DecorationMethod"" = @DecorationMethod,
                    ""Height"" = @Height,
                    ""Width"" = @Width";
            await _dbConnection.ExecuteAsync(query, placeholders);
        }

        public async Task DeleteByVariantIdsAsync(IEnumerable<int> variantIds)
        {
            const string query = @"DELETE FROM public.""PrintifyBlueprintVariantPlaceholders"" WHERE ""VariantId"" = ANY(@variantIds)";
            await _dbConnection.ExecuteAsync(query, new { variantIds });
        }
    }
}
