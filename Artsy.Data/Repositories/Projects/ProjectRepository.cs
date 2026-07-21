using Dapper;
using System.Data;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.Data.Repositories.Projects
{
    public class ProjectRepository : IProjectRepository
    {
        readonly IDbConnection _dbConnection;

        public ProjectRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<Project>> GetAllAsync(Guid appUserId)
        {
            const string query = @"SELECT * FROM public.""Projects"" WHERE ""AppUserId"" = @appUserId ORDER BY ""Created"" DESC";
            return await _dbConnection.QueryAsync<Project>(query, new { appUserId });
        }

        public async Task<Project?> GetByIdAsync(Guid id, Guid appUserId)
        {
            const string query = @"SELECT * FROM public.""Projects"" WHERE ""Id"" = @id AND ""AppUserId"" = @appUserId";
            return await _dbConnection.QueryFirstOrDefaultAsync<Project>(query, new { id, appUserId });
        }

        public async Task<Project?> GetByKeyAsync(string key)
        {
            const string query = @"SELECT * FROM public.""Projects"" WHERE ""Key"" = @key";
            return await _dbConnection.QueryFirstOrDefaultAsync<Project>(query, new { key });
        }

        public async Task<Project> CreateAsync(Project project)
        {
            const string query = @"
                INSERT INTO public.""Projects"" (""Id"", ""AppUserId"", ""Title"", ""Description"", ""Key"", ""Color"", ""Status"", ""PublishToPrintify"", ""Created"")
                VALUES (@Id, @AppUserId, @Title, @Description, @Key, @Color, @Status, @PublishToPrintify, @Created)
                RETURNING *";

            project.Id = Guid.NewGuid();
            project.Created = DateTime.UtcNow;
            project.Status = 1;
            return await _dbConnection.QuerySingleAsync<Project>(query, project);
        }

        public async Task UpdateAsync(Project project)
        {
            const string query = @"
                UPDATE public.""Projects"" SET
                    ""Title"" = @Title,
                    ""Description"" = @Description,
                    ""Key"" = @Key,
                    ""Color"" = @Color,
                    ""Status"" = @Status,
                    ""PublishToPrintify"" = @PublishToPrintify
                WHERE ""Id"" = @Id AND ""AppUserId"" = @AppUserId";

            await _dbConnection.ExecuteAsync(query, project);
        }

        public async Task DeleteAsync(Guid id, Guid appUserId)
        {
            const string query = @"DELETE FROM public.""Projects"" WHERE ""Id"" = @id AND ""AppUserId"" = @appUserId";
            await _dbConnection.ExecuteAsync(query, new { id, appUserId });
        }
    }
}
