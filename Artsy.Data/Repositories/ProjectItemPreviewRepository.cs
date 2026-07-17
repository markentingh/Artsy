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
            const string query = @"SELECT * FROM public.""ProjectItemPreviews"" WHERE ""ItemId"" = @itemId ORDER BY ""Created"" DESC";
            return await _dbConnection.QueryAsync<ProjectItemPreview>(query, new { itemId });
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
