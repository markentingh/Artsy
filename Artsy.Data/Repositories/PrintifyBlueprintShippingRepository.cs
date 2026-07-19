using Dapper;
using System.Data;
using Artsy.Data.Entities;
using Artsy.Data.Interfaces;

namespace Artsy.Data.Repositories
{
    public class PrintifyBlueprintShippingRepository : IPrintifyBlueprintShippingRepository
    {
        readonly IDbConnection _dbConnection;

        public PrintifyBlueprintShippingRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<PrintifyBlueprintShipping?> GetByBlueprintAndProviderAsync(int blueprintId, int printProviderId)
        {
            const string query = @"SELECT * FROM public.""PrintifyBlueprintShipping"" WHERE ""BlueprintId"" = @blueprintId AND ""PrintProviderId"" = @printProviderId";
            return await _dbConnection.QueryFirstOrDefaultAsync<PrintifyBlueprintShipping>(query, new { blueprintId, printProviderId });
        }

        public async Task UpsertAsync(PrintifyBlueprintShipping shipping)
        {
            const string query = @"
                INSERT INTO public.""PrintifyBlueprintShipping"" (""BlueprintId"", ""PrintProviderId"", ""HandlingTimeValue"", ""HandlingTimeUnit"", ""Profiles"", ""DateUpdated"")
                VALUES (@BlueprintId, @PrintProviderId, @HandlingTimeValue, @HandlingTimeUnit, @Profiles, CURRENT_TIMESTAMP)
                ON CONFLICT (""BlueprintId"", ""PrintProviderId"")
                DO UPDATE SET
                    ""HandlingTimeValue"" = @HandlingTimeValue,
                    ""HandlingTimeUnit"" = @HandlingTimeUnit,
                    ""Profiles"" = @Profiles,
                    ""DateUpdated"" = CURRENT_TIMESTAMP";
            await _dbConnection.ExecuteAsync(query, shipping);
        }

        public async Task DeleteByBlueprintAndProviderAsync(int blueprintId, int printProviderId)
        {
            const string query = @"DELETE FROM public.""PrintifyBlueprintShipping"" WHERE ""BlueprintId"" = @blueprintId AND ""PrintProviderId"" = @printProviderId";
            await _dbConnection.ExecuteAsync(query, new { blueprintId, printProviderId });
        }
    }
}
