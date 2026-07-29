using Dapper;
using System.Data;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.Data.Repositories.Projects
{
    public class ProjectBlueprintProductImageRepository : IProjectBlueprintProductImageRepository
    {
        readonly IDbConnection _dbConnection;

        public ProjectBlueprintProductImageRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<ProjectBlueprintProductImage?> GetByIdAsync(Guid id)
        {
            const string query = @"SELECT * FROM public.""ProjectBlueprintProductImages"" WHERE ""Id"" = @id";
            return await _dbConnection.QueryFirstOrDefaultAsync<ProjectBlueprintProductImage>(query, new { id });
        }

        public async Task<IEnumerable<ProjectBlueprintProductImage>> GetByProjectBlueprintIdAsync(Guid projectBlueprintId)
        {
            const string query = @"SELECT * FROM public.""ProjectBlueprintProductImages"" WHERE ""ProjectBlueprintId"" = @projectBlueprintId AND ""Status"" = 1 ORDER BY ""DateCreated""";
            return await _dbConnection.QueryAsync<ProjectBlueprintProductImage>(query, new { projectBlueprintId });
        }

        public async Task<IEnumerable<ProjectBlueprintProductImage>> GetByProjectIdAsync(Guid projectId)
        {
            const string query = @"SELECT * FROM public.""ProjectBlueprintProductImages"" WHERE ""ProjectId"" = @projectId AND ""Status"" = 1 ORDER BY ""DateCreated""";
            return await _dbConnection.QueryAsync<ProjectBlueprintProductImage>(query, new { projectId });
        }

        public async Task<IEnumerable<ProjectBlueprintProductImage>> GetByBlueprintIdsAsync(IEnumerable<Guid> blueprintIds)
        {
            var ids = blueprintIds.ToList();
            if (ids.Count == 0) return Enumerable.Empty<ProjectBlueprintProductImage>();
            const string query = @"SELECT * FROM public.""ProjectBlueprintProductImages"" WHERE ""ProjectBlueprintId"" = ANY(@blueprintIds) AND ""Status"" = 1 ORDER BY ""ProjectBlueprintId"", ""DateCreated""";
            return await _dbConnection.QueryAsync<ProjectBlueprintProductImage>(query, new { blueprintIds = ids.ToArray() });
        }

        public async Task<ProjectBlueprintProductImage> CreateAsync(ProjectBlueprintProductImage image)
        {
            image.Id = Guid.NewGuid();
            const string query = @"
                INSERT INTO public.""ProjectBlueprintProductImages"" (""Id"", ""ProjectId"", ""ProjectBlueprintId"", ""Title"", ""VariantColor"", ""Status"", ""Prompt"", ""DateCreated"", ""DateUpdated"")
                VALUES (@Id, @ProjectId, @ProjectBlueprintId, @Title, @VariantColor, @Status, @Prompt, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
                RETURNING *";
            return await _dbConnection.QueryFirstAsync<ProjectBlueprintProductImage>(query, image);
        }

        public async Task UpdateAsync(ProjectBlueprintProductImage image)
        {
            const string query = @"
                UPDATE public.""ProjectBlueprintProductImages""
                SET ""Title"" = @Title, ""VariantColor"" = @VariantColor, ""Prompt"" = @Prompt, ""DateUpdated"" = CURRENT_TIMESTAMP
                WHERE ""Id"" = @Id";
            await _dbConnection.ExecuteAsync(query, image);
        }

        public async Task DeleteAsync(Guid id)
        {
            const string query = @"DELETE FROM public.""ProjectBlueprintProductImages"" WHERE ""Id"" = @id";
            await _dbConnection.ExecuteAsync(query, new { id });
        }

        public async Task SetStatusAsync(Guid id, int status)
        {
            const string query = @"UPDATE public.""ProjectBlueprintProductImages"" SET ""Status"" = @status, ""DateUpdated"" = CURRENT_TIMESTAMP WHERE ""Id"" = @id";
            await _dbConnection.ExecuteAsync(query, new { id, status });
        }
    }
}
