using Dapper;
using System.Data;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.Data.Repositories.Projects
{
    public class ProjectCollectionProductRepository : IProjectCollectionProductRepository
    {
        readonly IDbConnection _dbConnection;

        public ProjectCollectionProductRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<ProjectCollectionProduct?> GetByIdAsync(Guid id)
        {
            const string query = @"SELECT * FROM public.""ProjectCollectionProducts"" WHERE ""Id"" = @id";
            return await _dbConnection.QueryFirstOrDefaultAsync<ProjectCollectionProduct>(query, new { id });
        }

        public async Task<ProjectCollectionProduct?> GetByCollectionAndBlueprintIdAsync(Guid collectionId, Guid projectBlueprintId)
        {
            const string query = @"SELECT * FROM public.""ProjectCollectionProducts"" WHERE ""CollectionId"" = @collectionId AND ""ProjectBlueprintId"" = @projectBlueprintId";
            return await _dbConnection.QueryFirstOrDefaultAsync<ProjectCollectionProduct>(query, new { collectionId, projectBlueprintId });
        }

        public async Task<IEnumerable<ProjectCollectionProduct>> GetByCollectionIdAsync(Guid collectionId)
        {
            const string query = @"SELECT * FROM public.""ProjectCollectionProducts"" WHERE ""CollectionId"" = @collectionId";
            return await _dbConnection.QueryAsync<ProjectCollectionProduct>(query, new { collectionId });
        }

        public async Task<ProjectCollectionProduct> CreateAsync(ProjectCollectionProduct product)
        {
            product.Id = Guid.NewGuid();
            const string query = @"
                INSERT INTO public.""ProjectCollectionProducts"" (""Id"", ""ProjectId"", ""CollectionId"", ""ProjectBlueprintId"", ""BlueprintId"", ""Name"", ""Description"", ""SafetyInfo"", ""PricingJson"")
                VALUES (@Id, @ProjectId, @CollectionId, @ProjectBlueprintId, @BlueprintId, @Name, @Description, @SafetyInfo, @PricingJson)
                RETURNING *";
            return await _dbConnection.QueryFirstAsync<ProjectCollectionProduct>(query, product);
        }

        public async Task UpdateAsync(ProjectCollectionProduct product)
        {
            const string query = @"
                UPDATE public.""ProjectCollectionProducts"" SET
                    ""Name"" = @Name,
                    ""Description"" = @Description,
                    ""SafetyInfo"" = @SafetyInfo,
                    ""PricingJson"" = @PricingJson
                WHERE ""Id"" = @Id";
            await _dbConnection.ExecuteAsync(query, product);
        }

        public async Task DeleteAsync(Guid id)
        {
            const string query = @"DELETE FROM public.""ProjectCollectionProducts"" WHERE ""Id"" = @id";
            await _dbConnection.ExecuteAsync(query, new { id });
        }
    }
}
