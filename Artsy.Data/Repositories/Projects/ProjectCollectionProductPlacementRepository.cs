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

        public async Task DeleteByProductIdAsync(Guid productId)
        {
            const string query = @"DELETE FROM public.""ProjectCollectionProductPlacements""
                WHERE ""ProductId"" = @productId";
            await _dbConnection.ExecuteAsync(query, new { productId });
        }

        public async Task CreateAsync(ProjectCollectionProductPlacement placement)
        {
            if (placement.Id == Guid.Empty) placement.Id = Guid.NewGuid();
            const string query = @"
                INSERT INTO public.""ProjectCollectionProductPlacements"" (""Id"", ""ProductId"", ""ArtworkId"", ""ArtworkPlacementId"", ""Position"", ""VariantIds"", ""PlacementIndex"")
                VALUES (@Id, @ProductId, @ArtworkId, @ArtworkPlacementId, @Position, @VariantIds, @PlacementIndex)";
            await _dbConnection.ExecuteAsync(query, placement);
        }
    }
}
