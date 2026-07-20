using Dapper;
using System.Data;
using Artsy.Data.Entities;
using Artsy.Data.Interfaces;

namespace Artsy.Data.Repositories
{
    public class PrintifyBlueprintImageRepository : IPrintifyBlueprintImageRepository
    {
        readonly IDbConnection _dbConnection;

        public PrintifyBlueprintImageRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<PrintifyBlueprintImage>> GetByBlueprintIdAsync(int blueprintId)
        {
            const string query = @"SELECT * FROM public.""PrintifyBlueprintImages"" WHERE ""BlueprintId"" = @blueprintId ORDER BY ""ImageIndex""";
            return await _dbConnection.QueryAsync<PrintifyBlueprintImage>(query, new { blueprintId });
        }

        public async Task UpsertAsync(PrintifyBlueprintImage image)
        {
            const string query = @"
                INSERT INTO public.""PrintifyBlueprintImages"" (""BlueprintId"", ""ImageIndex"", ""Variants"", ""Type"", ""Position"", ""DateCreated"", ""DateUpdated"")
                VALUES (@BlueprintId, @ImageIndex, @Variants, @Type, @Position, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
                ON CONFLICT (""BlueprintId"", ""ImageIndex"")
                DO UPDATE SET
                    ""Variants"" = @Variants,
                    ""Type"" = @Type,
                    ""Position"" = @Position,
                    ""DateUpdated"" = CURRENT_TIMESTAMP";
            await _dbConnection.ExecuteAsync(query, image);
        }
    }
}
