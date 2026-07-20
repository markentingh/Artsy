using Dapper;
using System.Data;
using Artsy.Data.Entities;
using Artsy.Data.Interfaces;

namespace Artsy.Data.Repositories
{
    public class PrintifyBlueprintPrintProviderRepository : IPrintifyBlueprintPrintProviderRepository
    {
        readonly IDbConnection _dbConnection;

        public PrintifyBlueprintPrintProviderRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<PrintifyBlueprintPrintProvider>> GetByBlueprintIdAsync(int blueprintId)
        {
            const string query = @"SELECT * FROM public.""PrintifyBlueprintPrintProviders"" WHERE ""BlueprintId"" = @blueprintId ORDER BY ""Title""";
            return await _dbConnection.QueryAsync<PrintifyBlueprintPrintProvider>(query, new { blueprintId });
        }

        public async Task UpsertBatchAsync(IEnumerable<PrintifyBlueprintPrintProvider> providers)
        {
            const string query = @"
                INSERT INTO public.""PrintifyBlueprintPrintProviders"" (""BlueprintId"", ""PrintProviderId"", ""Title"", ""DecorationMethods"", ""DateUpdated"")
                VALUES (@BlueprintId, @PrintProviderId, @Title, @DecorationMethods, CURRENT_TIMESTAMP)
                ON CONFLICT (""BlueprintId"", ""PrintProviderId"")
                DO UPDATE SET
                    ""Title"" = @Title,
                    ""DecorationMethods"" = @DecorationMethods,
                    ""DateUpdated"" = CURRENT_TIMESTAMP";
            await _dbConnection.ExecuteAsync(query, providers);
        }

        public async Task DeleteByBlueprintIdAsync(int blueprintId)
        {
            const string query = @"DELETE FROM public.""PrintifyBlueprintPrintProviders"" WHERE ""BlueprintId"" = @blueprintId";
            await _dbConnection.ExecuteAsync(query, new { blueprintId });
        }
    }
}
