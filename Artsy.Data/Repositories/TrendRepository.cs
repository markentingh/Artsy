using Artsy.Data.Entities;
using Artsy.Data.Interfaces;
using Dapper;
using System.Data;

namespace Artsy.Data.Repositories
{
    public class TrendRepository : ITrendRepository
    {
        readonly IDbConnection _dbConnection;

        public TrendRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<Trend>> GetRecentAsync(int limit = 20)
        {
            const string query = @"
                SELECT * FROM public.""Trends""
                ORDER BY ""DateCreated"" DESC
                LIMIT @limit";
            return await _dbConnection.QueryAsync<Trend>(query, new { limit });
        }

        public async Task<Trend> CreateAsync(Trend trend)
        {
            trend.Id = Guid.NewGuid();
            const string query = @"
                INSERT INTO public.""Trends"" (""Id"", ""Keyword"", ""Sector"", ""EtsyListingCount"", ""Data"")
                VALUES (@Id, @Keyword, @Sector, @EtsyListingCount, @Data)
                RETURNING *";
            return await _dbConnection.QueryFirstAsync<Trend>(query, trend);
        }

        public async Task DeleteAsync(Guid id)
        {
            const string query = @"DELETE FROM public.""Trends"" WHERE ""Id"" = @id";
            await _dbConnection.ExecuteAsync(query, new { id });
        }
    }
}
