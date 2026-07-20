using Dapper;
using System.Data;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.Data.Repositories.Projects
{
    public class ProjectItemReferenceRepository : IProjectItemReferenceRepository
    {
        readonly IDbConnection _dbConnection;

        public ProjectItemReferenceRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<ProjectItemReference>> GetByItemIdAsync(Guid itemId)
        {
            const string query = @"SELECT * FROM public.""ProjectItemReferences"" WHERE ""ItemId"" = @itemId ORDER BY ""Created""";
            return await _dbConnection.QueryAsync<ProjectItemReference>(query, new { itemId });
        }

        public async Task<IEnumerable<ProjectItemReference>> GetByProjectIdAsync(Guid projectId)
        {
            const string query = @"SELECT * FROM public.""ProjectItemReferences"" WHERE ""ProjectId"" = @projectId ORDER BY ""Created""";
            return await _dbConnection.QueryAsync<ProjectItemReference>(query, new { projectId });
        }

        public async Task<IEnumerable<ProjectItemThumbnailDto>> GetThumbnailsByProjectIdAsync(Guid projectId)
        {
            const string query = @"SELECT ""Id"", ""ItemId"" FROM public.""ProjectItemReferences"" WHERE ""ProjectId"" = @projectId ORDER BY ""Created""";
            return await _dbConnection.QueryAsync<ProjectItemThumbnailDto>(query, new { projectId });
        }

        public async Task<ProjectItemReference?> GetByIdAsync(Guid id)
        {
            const string query = @"SELECT * FROM public.""ProjectItemReferences"" WHERE ""Id"" = @id";
            return await _dbConnection.QueryFirstOrDefaultAsync<ProjectItemReference>(query, new { id });
        }

        public async Task<ProjectItemReference> CreateAsync(ProjectItemReference reference)
        {
            reference.Id = Guid.NewGuid();
            const string query = @"
                INSERT INTO public.""ProjectItemReferences"" (""Id"", ""ItemId"", ""ProjectId"", ""FileName"", ""Extension"", ""Created"")
                VALUES (@Id, @ItemId, @ProjectId, @FileName, @Extension, @Created)
                RETURNING *";
            return await _dbConnection.QueryFirstAsync<ProjectItemReference>(query, reference);
        }

        public async Task DeleteAsync(Guid id)
        {
            const string query = @"DELETE FROM public.""ProjectItemReferences"" WHERE ""Id"" = @id";
            await _dbConnection.ExecuteAsync(query, new { id });
        }
    }
}
