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

        public async Task<IEnumerable<ProjectCollectionProductImage>> FilterByProjectIdsAsync(Guid[] projectIds, int length = 5)
        {
            const string query = @"
                WITH ranked AS (
                    SELECT p.*, ROW_NUMBER() OVER (PARTITION BY p.""ProjectId"" ORDER BY c.""Created"" DESC, p.""Id"") AS rn
                    FROM public.""ProjectCollectionProductImages"" p
                    INNER JOIN public.""ProjectCollections"" c ON c.""Id"" = p.""CollectionId""
                    WHERE p.""ProjectId"" = ANY(@projectIds) AND p.""Active"" = TRUE AND c.""Status"" = 1
                )
                SELECT * FROM ranked WHERE rn <= @length";
            return await _dbConnection.QueryAsync<ProjectCollectionProductImage>(query, new { projectIds, length });
        }

        public async Task<ProjectCollectionProductImage?> GetByCollectionBlueprintVariantPlacementAsync(Guid collectionId, Guid projectBlueprintId, int variant, int placement)
        {
            const string query = @"SELECT * FROM public.""ProjectCollectionProductImages"" WHERE ""CollectionId"" = @collectionId AND ""ProjectBlueprintId"" = @projectBlueprintId AND ""Variant"" = @variant AND ""Placement"" = @placement AND ""Active"" = TRUE";
            return await _dbConnection.QueryFirstOrDefaultAsync<ProjectCollectionProductImage>(query, new { collectionId, projectBlueprintId, variant, placement });
        }

        public async Task<IEnumerable<ProjectCollectionProductImage>> GetByCollectionIdAsync(Guid collectionId)
        {
            const string query = @"SELECT * FROM public.""ProjectCollectionProductImages"" WHERE ""CollectionId"" = @collectionId AND ""Active"" = TRUE ORDER BY ""ProjectBlueprintId"", ""Variant"", ""Placement""";
            return await _dbConnection.QueryAsync<ProjectCollectionProductImage>(query, new { collectionId });
        }

        public async Task<IEnumerable<ProjectCollectionProductImage>> GetAllByCollectionIdAsync(Guid collectionId)
        {
            const string query = @"SELECT * FROM public.""ProjectCollectionProductImages"" WHERE ""CollectionId"" = @collectionId ORDER BY ""ProjectBlueprintId"", ""Variant"", ""Placement""";
            return await _dbConnection.QueryAsync<ProjectCollectionProductImage>(query, new { collectionId });
        }

        public async Task<IEnumerable<ProjectCollectionProductImage>> GetByCollectionAndBlueprintIdAsync(Guid collectionId, Guid projectBlueprintId)
        {
            const string query = @"SELECT * FROM public.""ProjectCollectionProductImages"" WHERE ""CollectionId"" = @collectionId AND ""ProjectBlueprintId"" = @projectBlueprintId AND ""Active"" = TRUE ORDER BY ""Variant"", ""Placement""";
            return await _dbConnection.QueryAsync<ProjectCollectionProductImage>(query, new { collectionId, projectBlueprintId });
        }

        public async Task<ProjectCollectionProductImage> CreateAsync(ProjectCollectionProductImage image)
        {
            image.Id = Guid.NewGuid();
            const string query = @"
                INSERT INTO public.""ProjectCollectionProductImages"" (""Id"", ""ProjectId"", ""CollectionId"", ""ProjectBlueprintId"", ""Variant"", ""Placement"", ""ImageModel"", ""Prompt"", ""Width"", ""Height"", ""Accepted"", ""ResponseId"", ""Active"")
                VALUES (@Id, @ProjectId, @CollectionId, @ProjectBlueprintId, @Variant, @Placement, @ImageModel, @Prompt, @Width, @Height, @Accepted, @ResponseId, @Active)
                RETURNING *";
            return await _dbConnection.QueryFirstAsync<ProjectCollectionProductImage>(query, image);
        }

        public async Task UpdateAsync(ProjectCollectionProductImage image)
        {
            const string query = @"
                UPDATE public.""ProjectCollectionProductImages""
                SET ""ImageModel"" = @ImageModel, ""Prompt"" = @Prompt, ""Width"" = @Width, ""Height"" = @Height,
                    ""Accepted"" = @Accepted, ""ResponseId"" = @ResponseId, ""Active"" = @Active
                WHERE ""Id"" = @Id";
            await _dbConnection.ExecuteAsync(query, image);
        }

        public async Task SetInactiveAsync(Guid collectionId, Guid projectBlueprintId, int variant, int placement)
        {
            const string query = @"UPDATE public.""ProjectCollectionProductImages"" SET ""Active"" = FALSE WHERE ""CollectionId"" = @collectionId AND ""ProjectBlueprintId"" = @projectBlueprintId AND ""Variant"" = @variant AND ""Placement"" = @placement";
            await _dbConnection.ExecuteAsync(query, new { collectionId, projectBlueprintId, variant, placement });
        }

        public async Task SetPrintifyImageIdAsync(Guid id, string printifyImageId)
        {
            const string query = @"UPDATE public.""ProjectCollectionProductImages"" SET ""PrintifyImageId"" = @printifyImageId WHERE ""Id"" = @id";
            await _dbConnection.ExecuteAsync(query, new { id, printifyImageId });
        }

        public async Task DeleteAsync(Guid id)
        {
            const string query = @"DELETE FROM public.""ProjectCollectionProductImages"" WHERE ""Id"" = @id";
            await _dbConnection.ExecuteAsync(query, new { id });
        }
    }
}
