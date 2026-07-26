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

        public async Task<IEnumerable<PrintifyBlueprintImageVariant>> GetByImageIdAsync(Guid imageId)
        {
            const string query = @"SELECT * FROM public.""PrintifyBlueprintImageVariants"" WHERE ""ImageId"" = @imageId";
            return await _dbConnection.QueryAsync<PrintifyBlueprintImageVariant>(query, new { imageId });
        }

        public async Task<IEnumerable<PrintifyBlueprintImageVariant>> GetByImageIdsAsync(IEnumerable<Guid> imageIds)
        {
            var ids = imageIds.ToList();
            if (ids.Count == 0) return Enumerable.Empty<PrintifyBlueprintImageVariant>();
            const string query = @"SELECT * FROM public.""PrintifyBlueprintImageVariants"" WHERE ""ImageId"" = ANY(@imageIds)";
            return await _dbConnection.QueryAsync<PrintifyBlueprintImageVariant>(query, new { imageIds = ids.ToArray() });
        }

        public async Task<IEnumerable<PrintifyBlueprintImageVariant>> GetByBlueprintIdsAsync(IEnumerable<int> blueprintIds)
        {
            var ids = blueprintIds.ToList();
            if (ids.Count == 0) return Enumerable.Empty<PrintifyBlueprintImageVariant>();
            const string query = @"
                SELECT piv.* 
                FROM public.""PrintifyBlueprintImageVariants"" piv
                INNER JOIN public.""PrintifyBlueprintImages"" pbi ON piv.""ImageId"" = pbi.""Id""
                WHERE pbi.""BlueprintId"" = ANY(@blueprintIds)";
            return await _dbConnection.QueryAsync<PrintifyBlueprintImageVariant>(query, new { blueprintIds = ids.ToArray() });
        }

        public async Task DeleteByImageIdAsync(Guid imageId)
        {
            const string query = @"DELETE FROM public.""PrintifyBlueprintImageVariants"" WHERE ""ImageId"" = @imageId";
            await _dbConnection.ExecuteAsync(query, new { imageId });
        }

        public async Task DeleteByImageAndVariantIdsAsync(Guid imageId, IEnumerable<int> variantIds)
        {
            var ids = variantIds.ToList();
            if (ids.Count == 0) return;
            const string query = @"DELETE FROM public.""PrintifyBlueprintImageVariants"" WHERE ""ImageId"" = @imageId AND ""VariantId"" = ANY(@variantIds)";
            await _dbConnection.ExecuteAsync(query, new { imageId, variantIds = ids.ToArray() });
        }

        public async Task InsertBatchAsync(IEnumerable<PrintifyBlueprintImageVariant> imageVariants)
        {
            const string query = @"
                INSERT INTO public.""PrintifyBlueprintImageVariants"" (""ImageId"", ""VariantId"")
                VALUES (@ImageId, @VariantId)
                ON CONFLICT (""ImageId"", ""VariantId"") DO NOTHING";
            await _dbConnection.ExecuteAsync(query, imageVariants);
        }
    }
}
