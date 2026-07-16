using Dapper;
using System.Data;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.Data.Repositories.Projects
{
    public class ProjectItemRepository : IProjectItemRepository
    {
        readonly IDbConnection _dbConnection;

        public ProjectItemRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<ProjectItem>> GetByProjectIdAsync(Guid projectId)
        {
            const string query = @"SELECT * FROM public.""ProjectItems"" WHERE ""ProjectId"" = @projectId ORDER BY ""Index""";
            return await _dbConnection.QueryAsync<ProjectItem>(query, new { projectId });
        }

        public async Task<ProjectItem?> GetByIdAsync(Guid id)
        {
            const string query = @"SELECT * FROM public.""ProjectItems"" WHERE ""Id"" = @id";
            return await _dbConnection.QueryFirstOrDefaultAsync<ProjectItem>(query, new { id });
        }

        public async Task<ProjectItem> CreateAsync(ProjectItem item)
        {
            item.Id = Guid.NewGuid();
            const string query = @"
                INSERT INTO public.""ProjectItems"" (""Id"", ""ProjectId"", ""Index"")
                VALUES (@Id, @ProjectId, @Index)
                RETURNING *";
            return await _dbConnection.QueryFirstAsync<ProjectItem>(query, item);
        }

        public async Task UpdateAsync(ProjectItem item)
        {
            const string query = @"
                UPDATE public.""ProjectItems""
                SET ""Index"" = @Index
                WHERE ""Id"" = @Id";
            await _dbConnection.ExecuteAsync(query, item);
        }

        public async Task DeleteAsync(Guid id)
        {
            const string query = @"DELETE FROM public.""ProjectItems"" WHERE ""Id"" = @id";
            await _dbConnection.ExecuteAsync(query, new { id });
        }
    }
}
