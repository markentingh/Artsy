using Dapper;
using System.Data;
using Artsy.Data.Entities;
using Artsy.Data.Interfaces;

namespace Artsy.Data.Repositories
{
    public class PrintifyBlueprintImageVariantRepository : IPrintifyBlueprintImageVariantRepository
    {
        readonly IDbConnection _dbConnection;

        public PrintifyBlueprintImageVariantRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<PrintifyBlueprintImageVariant>> GetByBlueprintImageIdAsync(Guid blueprintImageId)
        {
            const string query = @"SELECT * FROM public.""PrintifyBlueprintImageVariants"" WHERE ""BlueprintImageId"" = @blueprintImageId ORDER BY ""VariantColor""";
            return await _dbConnection.QueryAsync<PrintifyBlueprintImageVariant>(query, new { blueprintImageId });
        }

        public async Task<IEnumerable<PrintifyBlueprintImageVariant>> GetByBlueprintImageIdsAsync(IEnumerable<Guid> blueprintImageIds)
        {
            var ids = blueprintImageIds.ToList();
            if (ids.Count == 0) return Enumerable.Empty<PrintifyBlueprintImageVariant>();
            const string query = @"SELECT * FROM public.""PrintifyBlueprintImageVariants"" WHERE ""BlueprintImageId"" = ANY(@blueprintImageIds) ORDER BY ""BlueprintImageId"", ""VariantColor""";
            return await _dbConnection.QueryAsync<PrintifyBlueprintImageVariant>(query, new { blueprintImageIds = ids.ToArray() });
        }

        public async Task UpsertAsync(Guid blueprintImageId, string variantColor)
        {
            const string query = @"
                INSERT INTO public.""PrintifyBlueprintImageVariants"" (""BlueprintImageId"", ""VariantColor"", ""DateCreated"", ""DateUpdated"")
                VALUES (@blueprintImageId, @variantColor, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
                ON CONFLICT (""BlueprintImageId"", ""VariantColor"")
                DO NOTHING";
            await _dbConnection.ExecuteAsync(query, new { blueprintImageId, variantColor });
        }

        public async Task DeleteByBlueprintImageIdAsync(Guid blueprintImageId)
        {
            const string query = @"DELETE FROM public.""PrintifyBlueprintImageVariants"" WHERE ""BlueprintImageId"" = @blueprintImageId";
            await _dbConnection.ExecuteAsync(query, new { blueprintImageId });
        }

        public async Task DeleteByBlueprintImageIdsAsync(IEnumerable<Guid> blueprintImageIds)
        {
            var ids = blueprintImageIds.ToList();
            if (ids.Count == 0) return;
            const string query = @"DELETE FROM public.""PrintifyBlueprintImageVariants"" WHERE ""BlueprintImageId"" = ANY(@blueprintImageIds)";
            await _dbConnection.ExecuteAsync(query, new { blueprintImageIds = ids.ToArray() });
        }

        public async Task DeleteAsync(Guid blueprintImageId, string variantColor)
        {
            const string query = @"DELETE FROM public.""PrintifyBlueprintImageVariants"" WHERE ""BlueprintImageId"" = @blueprintImageId AND ""VariantColor"" = @variantColor";
            await _dbConnection.ExecuteAsync(query, new { blueprintImageId, variantColor });
        }

        public async Task UpsertAsync(Guid blueprintImageId, IEnumerable<string> variantColors)
        {
            var colors = variantColors.ToList();
            if (colors.Count == 0) return;
            const string query = @"
                INSERT INTO public.""PrintifyBlueprintImageVariants"" (""BlueprintImageId"", ""VariantColor"", ""DateCreated"", ""DateUpdated"")
                VALUES (@blueprintImageId, @variantColor, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
                ON CONFLICT (""BlueprintImageId"", ""VariantColor"")
                DO NOTHING";
            await _dbConnection.ExecuteAsync(query, colors.Select(c => new { blueprintImageId, variantColor = c }));
        }

        public async Task DeleteAsync(Guid blueprintImageId, IEnumerable<string> variantColors)
        {
            var colors = variantColors.ToList();
            if (colors.Count == 0) return;
            const string query = @"DELETE FROM public.""PrintifyBlueprintImageVariants"" WHERE ""BlueprintImageId"" = @blueprintImageId AND ""VariantColor"" = ANY(@variantColors)";
            await _dbConnection.ExecuteAsync(query, new { blueprintImageId, variantColors = colors.ToArray() });
        }
    }
}
