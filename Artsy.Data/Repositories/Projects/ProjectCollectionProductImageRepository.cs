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

        public async Task<ProjectCollectionProductImage?> GetByCollectionBlueprintProductImageIdAsync(Guid collectionId, Guid projectBlueprintId, Guid productImageId, bool activeOnly = true)
        {
            var activeFilter = activeOnly ? @" AND ""Active"" = TRUE" : "";
            var query = $@"SELECT * FROM public.""ProjectCollectionProductImages"" WHERE ""CollectionId"" = @collectionId AND ""ProjectBlueprintId"" = @projectBlueprintId AND ""ProductImageId"" = @productImageId{activeFilter}";
            return await _dbConnection.QueryFirstOrDefaultAsync<ProjectCollectionProductImage>(query, new { collectionId, projectBlueprintId, productImageId });
        }

        public async Task<IEnumerable<ProjectCollectionProductImage>> GetByCollectionIdAsync(Guid collectionId)
        {
            const string query = @"SELECT p.* FROM public.""ProjectCollectionProductImages"" p LEFT JOIN public.""ProjectBlueprintProductImages"" b ON b.""Id"" = p.""ProductImageId"" WHERE p.""CollectionId"" = @collectionId AND p.""Active"" = TRUE ORDER BY p.""ProjectBlueprintId"", p.""ProductImageId""";
            return await _dbConnection.QueryAsync<ProjectCollectionProductImage>(query, new { collectionId });
        }

        public async Task<IEnumerable<ProjectCollectionProductImage>> GetAllByCollectionIdAsync(Guid collectionId)
        {
            const string query = @"SELECT * FROM public.""ProjectCollectionProductImages"" WHERE ""CollectionId"" = @collectionId ORDER BY ""ProjectBlueprintId"", ""ProductImageId""";
            return await _dbConnection.QueryAsync<ProjectCollectionProductImage>(query, new { collectionId });
        }

        public async Task<IEnumerable<ProjectCollectionProductImage>> GetByCollectionAndBlueprintIdAsync(Guid collectionId, Guid projectBlueprintId)
        {
            const string query = @"SELECT * FROM public.""ProjectCollectionProductImages"" WHERE ""CollectionId"" = @collectionId AND ""ProjectBlueprintId"" = @projectBlueprintId AND ""Active"" = TRUE ORDER BY ""ProductImageId""";
            return await _dbConnection.QueryAsync<ProjectCollectionProductImage>(query, new { collectionId, projectBlueprintId });
        }

        public async Task<IEnumerable<ProjectCollectionProductImage>> GetByPrintifyProductIdAsync(string printifyProductId)
        {
            const string query = @"
                SELECT pcpi.*
                FROM public.""ProjectCollectionPrintifyProducts"" pcpp
                JOIN public.""ProjectCollectionProducts"" pcp ON pcp.""Id"" = pcpp.""ProductId""
                JOIN public.""ProjectCollectionProductImages"" pcpi ON pcpi.""CollectionId"" = pcpp.""CollectionId"" AND pcpi.""ProjectBlueprintId"" = pcp.""ProjectBlueprintId""
                WHERE pcpp.""PrintifyProductId"" = @printifyProductId AND pcpp.""Status"" = 1 AND pcpi.""Active"" = TRUE
                ORDER BY pcpi.""ProductImageId""";
            return await _dbConnection.QueryAsync<ProjectCollectionProductImage>(query, new { printifyProductId });
        }

        public async Task<ProjectCollectionProductImage> CreateAsync(ProjectCollectionProductImage image)
        {
            image.Id = Guid.NewGuid();
            const string query = @"
                INSERT INTO public.""ProjectCollectionProductImages"" (""Id"", ""ProjectId"", ""CollectionId"", ""ProjectBlueprintId"", ""ProductImageId"", ""ImageModel"", ""Prompt"", ""Width"", ""Height"", ""Accepted"", ""ResponseId"", ""VariantColor"", ""Active"", ""SelectedMockups"", ""Generated"")
                VALUES (@Id, @ProjectId, @CollectionId, @ProjectBlueprintId, @ProductImageId, @ImageModel, @Prompt, @Width, @Height, @Accepted, @ResponseId, @VariantColor, @Active, @SelectedMockups, @Generated)
                RETURNING *";
            return await _dbConnection.QueryFirstAsync<ProjectCollectionProductImage>(query, image);
        }

        public async Task UpdateAsync(ProjectCollectionProductImage image)
        {
            const string query = @"
                UPDATE public.""ProjectCollectionProductImages""
                SET ""ImageModel"" = @ImageModel, ""Prompt"" = @Prompt, ""Width"" = @Width, ""Height"" = @Height,
                    ""Accepted"" = @Accepted, ""ResponseId"" = @ResponseId, ""VariantColor"" = @VariantColor, ""Active"" = @Active,
                    ""SelectedMockups"" = @SelectedMockups, ""Generated"" = @Generated
                WHERE ""Id"" = @Id";
            await _dbConnection.ExecuteAsync(query, image);
        }

        public async Task SetInactiveAsync(Guid collectionId, Guid projectBlueprintId, Guid productImageId)
        {
            const string query = @"UPDATE public.""ProjectCollectionProductImages"" SET ""Active"" = FALSE WHERE ""CollectionId"" = @collectionId AND ""ProjectBlueprintId"" = @projectBlueprintId AND ""ProductImageId"" = @productImageId";
            await _dbConnection.ExecuteAsync(query, new { collectionId, projectBlueprintId, productImageId });
        }

        public async Task DeleteByCollectionAndBlueprintIdAsync(Guid collectionId, Guid projectBlueprintId)
        {
            const string query = @"DELETE FROM public.""ProjectCollectionProductImages"" WHERE ""CollectionId"" = @collectionId AND ""ProjectBlueprintId"" = @projectBlueprintId";
            await _dbConnection.ExecuteAsync(query, new { collectionId, projectBlueprintId });
        }

        public async Task DeleteAsync(Guid id)
        {
            const string query = @"DELETE FROM public.""ProjectCollectionProductImages"" WHERE ""Id"" = @id";
            await _dbConnection.ExecuteAsync(query, new { id });
        }

        public async Task UpdateActiveAsync(Guid id, bool active)
        {
            const string query = @"
        UPDATE public.""ProjectCollectionProductImages""
        SET ""Active"" = @active
        WHERE ""Id"" = @id";
            await _dbConnection.ExecuteAsync(query, new { id, active });
        }

        public async Task UpdateAcceptedAsync(Guid id, bool accepted)
        {
            const string query = @"
        UPDATE public.""ProjectCollectionProductImages""
        SET ""Accepted"" = @accepted
        WHERE ""Id"" = @id";
            await _dbConnection.ExecuteAsync(query, new { id, accepted });
        }

        public async Task UpdateConfigAsync(Guid id, string variantColor, string imageModel, string prompt, string selectedMockups)
        {
            const string query = @"
        UPDATE public.""ProjectCollectionProductImages""
        SET ""VariantColor"" = @variantColor, ""ImageModel"" = @imageModel, ""Prompt"" = @prompt, ""SelectedMockups"" = @selectedMockups
        WHERE ""Id"" = @id";
            await _dbConnection.ExecuteAsync(query, new { id, variantColor, imageModel, prompt, selectedMockups });
        }

        public async Task SetGeneratedAsync(Guid id, bool generated)
        {
            const string query = @"
        UPDATE public.""ProjectCollectionProductImages""
        SET ""Generated"" = @generated
        WHERE ""Id"" = @id";
            await _dbConnection.ExecuteAsync(query, new { id, generated });
        }
    }
}
