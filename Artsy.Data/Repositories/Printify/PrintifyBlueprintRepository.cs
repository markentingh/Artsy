using Dapper;
using System.Data;
using Artsy.Data.Entities;
using Artsy.Data.Interfaces;

namespace Artsy.Data.Repositories
{
    public class PrintifyBlueprintRepository : IPrintifyBlueprintRepository
    {
        readonly IDbConnection _dbConnection;

        public PrintifyBlueprintRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<int> GetCountAsync()
        {
            const string query = @"SELECT COUNT(*) FROM public.""PrintifyBlueprints""";
            return await _dbConnection.ExecuteScalarAsync<int>(query);
        }

        public async Task<int> GetCountAsync(string keyword, string brand)
        {
            var kw = keyword?.Trim().ToLowerInvariant() ?? "";
            var br = brand?.Trim().ToLowerInvariant() ?? "";
            var sql = @"SELECT COUNT(*) FROM public.""PrintifyBlueprints"" WHERE ""Published"" = true";
            if (!string.IsNullOrWhiteSpace(kw))
                sql += @" AND LOWER(""Title"") LIKE @kw";
            if (!string.IsNullOrWhiteSpace(br) && br != "all")
                sql += @" AND LOWER(""Brand"") = @br";
            return await _dbConnection.ExecuteScalarAsync<int>(sql, new { kw = $"%{kw}%", br });
        }

        public async Task<IEnumerable<PrintifyBlueprint>> SearchAsync(string keyword, string brand, int start, int length)
        {
            var kw = keyword?.Trim().ToLowerInvariant() ?? "";
            var br = brand?.Trim().ToLowerInvariant() ?? "";
            var sql = @"SELECT * FROM public.""PrintifyBlueprints"" WHERE ""Published"" = true";
            if (!string.IsNullOrWhiteSpace(kw))
                sql += @" AND LOWER(""Title"") LIKE @kw";
            if (!string.IsNullOrWhiteSpace(br) && br != "all")
                sql += @" AND LOWER(""Brand"") = @br";
            sql += @" ORDER BY ""DateUpdated"" DESC LIMIT @length OFFSET @start";
            return await _dbConnection.QueryAsync<PrintifyBlueprint>(sql, new { kw = $"%{kw}%", br, start, length });
        }

        public async Task<PrintifyBlueprint?> GetByBlueprintIdAsync(int blueprintId)
        {
            const string query = @"SELECT * FROM public.""PrintifyBlueprints"" WHERE ""BlueprintId"" = @blueprintId";
            return await _dbConnection.QueryFirstOrDefaultAsync<PrintifyBlueprint>(query, new { blueprintId });
        }

        public async Task UpsertAsync(PrintifyBlueprint blueprint)
        {
            const string query = @"
                INSERT INTO public.""PrintifyBlueprints"" (""BlueprintId"", ""Title"", ""Description"", ""Brand"", ""Model"", ""ImageCount"", ""DateCreated"", ""DateUpdated"")
                VALUES (@BlueprintId, @Title, @Description, @Brand, @Model, @ImageCount, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
                ON CONFLICT (""BlueprintId"")
                DO UPDATE SET
                    ""Title"" = @Title,
                    ""Description"" = @Description,
                    ""Brand"" = @Brand,
                    ""Model"" = @Model,
                    ""ImageCount"" = @ImageCount,
                    ""DateUpdated"" = CURRENT_TIMESTAMP";
            await _dbConnection.ExecuteAsync(query, blueprint);
        }

        public async Task UpsertBatchAsync(IEnumerable<PrintifyBlueprint> blueprints)
        {
            const string query = @"
                INSERT INTO public.""PrintifyBlueprints"" (""BlueprintId"", ""Title"", ""Description"", ""Brand"", ""Model"", ""ImageCount"", ""DateCreated"", ""DateUpdated"")
                VALUES (@BlueprintId, @Title, @Description, @Brand, @Model, @ImageCount, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
                ON CONFLICT (""BlueprintId"")
                DO UPDATE SET
                    ""Title"" = @Title,
                    ""Description"" = @Description,
                    ""Brand"" = @Brand,
                    ""Model"" = @Model,
                    ""ImageCount"" = @ImageCount,
                    ""DateUpdated"" = CURRENT_TIMESTAMP";
            await _dbConnection.ExecuteAsync(query, blueprints);
        }

        public async Task<IEnumerable<string>> GetBrandsAsync()
        {
            const string query = @"SELECT DISTINCT ""Brand"" FROM public.""PrintifyBlueprints"" WHERE ""Brand"" != '' AND ""Published"" = true ORDER BY ""Brand""";
            return await _dbConnection.QueryAsync<string>(query);
        }

        public async Task<IEnumerable<int>> GetAllBlueprintIdsAsync()
        {
            const string query = @"SELECT ""BlueprintId"" FROM public.""PrintifyBlueprints""";
            return await _dbConnection.QueryAsync<int>(query);
        }

        public async Task DeleteAllAsync()
        {
            const string query = @"DELETE FROM public.""PrintifyBlueprints""";
            await _dbConnection.ExecuteAsync(query);
        }

        public async Task UpdatePublishedAsync(int blueprintId, bool published)
        {
            const string query = @"UPDATE public.""PrintifyBlueprints"" SET ""Published"" = @published, ""DateUpdated"" = CURRENT_TIMESTAMP WHERE ""BlueprintId"" = @blueprintId";
            await _dbConnection.ExecuteAsync(query, new { blueprintId, published });
        }
    }
}
