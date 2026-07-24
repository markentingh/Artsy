using Dapper;
using System.Data;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.Data.Repositories.Projects
{
    public class ProjectCollectionRepository : IProjectCollectionRepository
    {
        readonly IDbConnection _dbConnection;

        public ProjectCollectionRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<ProjectCollection>> GetByProjectIdAsync(Guid projectId)
        {
            const string query = @"
                SELECT * FROM public.""ProjectCollections""
                WHERE ""ProjectId"" = @projectId AND ""Status"" = 1
                ORDER BY ""Created"" DESC";
            return await _dbConnection.QueryAsync<ProjectCollection>(query, new { projectId });
        }

        public async Task<ProjectCollection?> GetByIdAsync(Guid id)
        {
            const string query = @"SELECT * FROM public.""ProjectCollections"" WHERE ""Id"" = @id";
            return await _dbConnection.QueryFirstOrDefaultAsync<ProjectCollection>(query, new { id });
        }

        public async Task<ProjectCollection> CreateAsync(ProjectCollection collection)
        {
            collection.Id = Guid.NewGuid();
            const string query = @"
                INSERT INTO public.""ProjectCollections"" (""Id"", ""ProjectId"", ""Title"", ""Created"")
                VALUES (@Id, @ProjectId, @Title, CURRENT_TIMESTAMP)
                RETURNING *";
            return await _dbConnection.QueryFirstAsync<ProjectCollection>(query, collection);
        }

        public async Task UpdateAsync(ProjectCollection collection)
        {
            const string query = @"
                UPDATE public.""ProjectCollections""
                SET ""Title"" = @Title
                WHERE ""Id"" = @Id";
            await _dbConnection.ExecuteAsync(query, collection);
        }

        public async Task DeleteAsync(Guid id)
        {
            const string query = @"UPDATE public.""ProjectCollections"" SET ""Status"" = 0 WHERE ""Id"" = @id";
            await _dbConnection.ExecuteAsync(query, new { id });
        }
    }
}
