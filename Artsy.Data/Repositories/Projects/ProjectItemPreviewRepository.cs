using Dapper;
using System.Data;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.Data.Repositories.Projects
{
    public class ProjectItemPreviewRepository : IProjectItemPreviewRepository
    {
        readonly IDbConnection _dbConnection;

        public ProjectItemPreviewRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<ProjectItemPreview>> GetByItemIdAsync(Guid itemId)
        {
            const string query = @"
                SELECT p.* FROM public.""ProjectItemPreviews"" p
                INNER JOIN public.""ProjectItems"" i ON i.""Id"" = p.""ItemId""
                WHERE p.""ItemId"" = @itemId AND i.""Status"" = 1
                ORDER BY p.""Created"" DESC";
            return await _dbConnection.QueryAsync<ProjectItemPreview>(query, new { itemId });
        }

        public async Task<IEnumerable<ProjectItemPreview>> GetByProjectIdAsync(Guid projectId)
        {
            const string query = @"
                SELECT p.* FROM public.""ProjectItemPreviews"" p
                INNER JOIN public.""ProjectItems"" i ON i.""Id"" = p.""ItemId""
                WHERE p.""ProjectId"" = @projectId AND i.""Status"" = 1
                ORDER BY p.""Created"" DESC";
            return await _dbConnection.QueryAsync<ProjectItemPreview>(query, new { projectId });
        }

        public async Task<IEnumerable<ProjectItemThumbnailDto>> GetThumbnailsByProjectIdAsync(Guid projectId)
        {
            const string query = @"
                SELECT p.""Id"", p.""ItemId"" FROM public.""ProjectItemPreviews"" p
                INNER JOIN public.""ProjectItems"" i ON i.""Id"" = p.""ItemId""
                WHERE p.""ProjectId"" = @projectId AND i.""Status"" = 1
                ORDER BY p.""Created"" DESC";
            return await _dbConnection.QueryAsync<ProjectItemThumbnailDto>(query, new { projectId });
        }

        public async Task<IEnumerable<ProjectItemThumbnailDto>> GetThumbnailsByProjectIdsAsync(Guid[] projectIds, int length = 5)
        {
            const string query = @"
                WITH ranked AS (
                    SELECT p.""Id"", p.""ItemId"", p.""ProjectId"", ROW_NUMBER() OVER (PARTITION BY p.""ProjectId"" ORDER BY p.""Created"" DESC) AS rn
                    FROM public.""ProjectItemPreviews"" p
                    INNER JOIN public.""ProjectItems"" i ON i.""Id"" = p.""ItemId""
                    WHERE p.""ProjectId"" = ANY(@projectIds) AND i.""Status"" = 1
                )
                SELECT ""Id"", ""ItemId"", ""ProjectId"" FROM ranked WHERE rn <= @length";
            return await _dbConnection.QueryAsync<ProjectItemThumbnailDto>(query, new { projectIds, length });
        }

        public async Task<ProjectItemPreview?> GetByIdAsync(Guid id)
        {
            const string query = @"SELECT * FROM public.""ProjectItemPreviews"" WHERE ""Id"" = @id";
            return await _dbConnection.QueryFirstOrDefaultAsync<ProjectItemPreview>(query, new { id });
        }

        public async Task<ProjectItemPreview> CreateAsync(ProjectItemPreview preview)
        {
            preview.Id = Guid.NewGuid();
            preview.Created = DateTime.UtcNow;
            const string query = @"
                INSERT INTO public.""ProjectItemPreviews"" (""Id"", ""ProjectId"", ""ItemId"", ""Created"", ""ImageModel"", ""ImageModelJson"")
                VALUES (@Id, @ProjectId, @ItemId, @Created, @ImageModel, @ImageModelJson)
                RETURNING *";
            return await _dbConnection.QueryFirstAsync<ProjectItemPreview>(query, preview);
        }

        public async Task DeleteAsync(Guid id)
        {
            const string query = @"DELETE FROM public.""ProjectItemPreviews"" WHERE ""Id"" = @id";
            await _dbConnection.ExecuteAsync(query, new { id });
        }
    }
}
