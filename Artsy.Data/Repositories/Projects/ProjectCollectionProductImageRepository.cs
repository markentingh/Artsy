using Dapper;
using System.Data;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.Data.Repositories.Projects
{
    public class ProjectCollectionProductImageRepository : IProjectCollectionProductImageRepository
    {
        readonly IDbConnection _dbConnection;

        public ProjectCollectionProductImageRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<ProjectCollectionProductImage?> GetByIdAsync(Guid id)
        {
            const string query = @"SELECT * FROM public.""ProjectCollectionProductImages"" WHERE ""Id"" = @id";
            return await _dbConnection.QueryFirstOrDefaultAsync<ProjectCollectionProductImage>(query, new { id });
        }

        public async Task<ProjectCollectionProductImage?> GetByCollectionBlueprintVariantPlacementAsync(Guid collectionId, Guid projectBlueprintId, int variant, int placement)
        {
            const string query = @"SELECT * FROM public.""ProjectCollectionProductImages"" WHERE ""CollectionId"" = @collectionId AND ""ProjectBlueprintId"" = @projectBlueprintId AND ""Variant"" = @variant AND ""Placement"" = @placement";
            return await _dbConnection.QueryFirstOrDefaultAsync<ProjectCollectionProductImage>(query, new { collectionId, projectBlueprintId, variant, placement });
        }

        public async Task<IEnumerable<ProjectCollectionProductImage>> GetByCollectionIdAsync(Guid collectionId)
        {
            const string query = @"SELECT * FROM public.""ProjectCollectionProductImages"" WHERE ""CollectionId"" = @collectionId ORDER BY ""ProjectBlueprintId"", ""Variant"", ""Placement""";
            return await _dbConnection.QueryAsync<ProjectCollectionProductImage>(query, new { collectionId });
        }

        public async Task<IEnumerable<ProjectCollectionProductImage>> GetByCollectionAndBlueprintIdAsync(Guid collectionId, Guid projectBlueprintId)
        {
            const string query = @"SELECT * FROM public.""ProjectCollectionProductImages"" WHERE ""CollectionId"" = @collectionId AND ""ProjectBlueprintId"" = @projectBlueprintId ORDER BY ""Variant"", ""Placement""";
            return await _dbConnection.QueryAsync<ProjectCollectionProductImage>(query, new { collectionId, projectBlueprintId });
        }

        public async Task<ProjectCollectionProductImage> CreateAsync(ProjectCollectionProductImage image)
        {
            image.Id = Guid.NewGuid();
            const string query = @"
                INSERT INTO public.""ProjectCollectionProductImages"" (""Id"", ""ProjectId"", ""CollectionId"", ""ProjectBlueprintId"", ""Variant"", ""Placement"", ""ImageModel"", ""Prompt"", ""Width"", ""Height"", ""Accepted"", ""ResponseId"")
                VALUES (@Id, @ProjectId, @CollectionId, @ProjectBlueprintId, @Variant, @Placement, @ImageModel, @Prompt, @Width, @Height, @Accepted, @ResponseId)
                RETURNING *";
            return await _dbConnection.QueryFirstAsync<ProjectCollectionProductImage>(query, image);
        }

        public async Task UpdateAsync(ProjectCollectionProductImage image)
        {
            const string query = @"
                UPDATE public.""ProjectCollectionProductImages""
                SET ""ImageModel"" = @ImageModel, ""Prompt"" = @Prompt, ""Width"" = @Width, ""Height"" = @Height,
                    ""Accepted"" = @Accepted, ""ResponseId"" = @ResponseId
                WHERE ""Id"" = @Id";
            await _dbConnection.ExecuteAsync(query, image);
        }

        public async Task DeleteAsync(Guid id)
        {
            const string query = @"DELETE FROM public.""ProjectCollectionProductImages"" WHERE ""Id"" = @id";
            await _dbConnection.ExecuteAsync(query, new { id });
        }
    }
}
