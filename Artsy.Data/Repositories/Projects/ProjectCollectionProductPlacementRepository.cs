using System.Data;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Projects;
using Dapper;

namespace Artsy.Data.Repositories.Projects
{
    public class ProjectCollectionProductPlacementRepository : IProjectCollectionProductPlacementRepository
    {
        private readonly IDbConnection _dbConnection;

        public ProjectCollectionProductPlacementRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<ProjectCollectionProductPlacement>> GetByProductIdAsync(Guid productId)
        {
            const string query = @"SELECT * FROM public.""ProjectCollectionProductPlacements"" WHERE ""ProductId"" = @productId";
            return await _dbConnection.QueryAsync<ProjectCollectionProductPlacement>(query, new { productId });
        }

        public async Task<IEnumerable<ProjectCollectionProductPlacement>> GetByProductIdAndVariantIdAsync(Guid productId, int variantId)
        {
            var all = await GetByProductIdAsync(productId);
            return all.Where(p =>
            {
                if (string.IsNullOrWhiteSpace(p.VariantIds) || p.VariantIds == "[]")
                    return false;
                try
                {
                    var ids = System.Text.Json.JsonSerializer.Deserialize<List<int>>(p.VariantIds) ?? new List<int>();
                    return ids.Contains(variantId);
                }
                catch
                {
                    return false;
                }
            });
        }
    }
}
