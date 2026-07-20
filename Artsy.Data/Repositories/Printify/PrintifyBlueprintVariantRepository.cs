using Dapper;
using System.Data;
using Artsy.Data.Entities;
using Artsy.Data.Interfaces;

namespace Artsy.Data.Repositories
{
    public class PrintifyBlueprintVariantRepository : IPrintifyBlueprintVariantRepository
    {
        readonly IDbConnection _dbConnection;

        public PrintifyBlueprintVariantRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<PrintifyBlueprintVariant>> GetByBlueprintAndProviderAsync(int blueprintId, int printProviderId)
        {
            const string query = @"SELECT * FROM public.""PrintifyBlueprintVariants"" WHERE ""BlueprintId"" = @blueprintId AND ""PrintProviderId"" = @printProviderId ORDER BY ""Title""";
            return await _dbConnection.QueryAsync<PrintifyBlueprintVariant>(query, new { blueprintId, printProviderId });
        }

        public async Task UpsertBatchAsync(IEnumerable<PrintifyBlueprintVariant> variants)
        {
            const string query = @"
                INSERT INTO public.""PrintifyBlueprintVariants"" (""VariantId"", ""BlueprintId"", ""PrintProviderId"", ""Title"", ""Options"", ""DecorationMethods"", ""DateUpdated"")
                VALUES (@VariantId, @BlueprintId, @PrintProviderId, @Title, @Options, @DecorationMethods, CURRENT_TIMESTAMP)
                ON CONFLICT (""VariantId"")
                DO UPDATE SET
                    ""BlueprintId"" = @BlueprintId,
                    ""PrintProviderId"" = @PrintProviderId,
                    ""Title"" = @Title,
                    ""Options"" = @Options,
                    ""DecorationMethods"" = @DecorationMethods,
                    ""DateUpdated"" = CURRENT_TIMESTAMP";
            await _dbConnection.ExecuteAsync(query, variants);
        }

        public async Task DeleteByBlueprintAndProviderAsync(int blueprintId, int printProviderId)
        {
            const string query = @"DELETE FROM public.""PrintifyBlueprintVariants"" WHERE ""BlueprintId"" = @blueprintId AND ""PrintProviderId"" = @printProviderId";
            await _dbConnection.ExecuteAsync(query, new { blueprintId, printProviderId });
        }
    }
}
