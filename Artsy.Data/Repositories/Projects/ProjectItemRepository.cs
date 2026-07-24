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
            const string query = @"SELECT * FROM public.""ProjectItems"" WHERE ""ProjectId"" = @projectId AND ""Status"" = 1 ORDER BY ""Index""";
            return await _dbConnection.QueryAsync<ProjectItem>(query, new { projectId });
        }

        public async Task<IEnumerable<ProjectItemListDto>> GetListByProjectIdAsync(Guid projectId)
        {
            const string query = @"
                SELECT i.""Id"", i.""ProjectId"", i.""Index"", i.""Title"", i.""SocialMedia"",
                    0 AS ""ProductCount"",
                    (SELECT COUNT(*) FROM public.""ProjectItemQuestions"" q WHERE q.""ItemId"" = i.""Id"") AS ""QuestionCount""
                FROM public.""ProjectItems"" i
                WHERE i.""ProjectId"" = @projectId AND i.""Status"" = 1
                ORDER BY i.""Index""";
            return await _dbConnection.QueryAsync<ProjectItemListDto>(query, new { projectId });
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
                INSERT INTO public.""ProjectItems"" (""Id"", ""ProjectId"", ""Index"", ""Title"")
                VALUES (@Id, @ProjectId, @Index, @Title)
                RETURNING *";
            return await _dbConnection.QueryFirstAsync<ProjectItem>(query, item);
        }

        public async Task UpdateAsync(ProjectItem item)
        {
            const string query = @"
                UPDATE public.""ProjectItems""
                SET ""Index"" = @Index, ""Title"" = @Title, ""SocialMedia"" = @SocialMedia
                WHERE ""Id"" = @Id";
            await _dbConnection.ExecuteAsync(query, item);
        }

        public async Task ReorderAsync(IEnumerable<Guid> itemIds)
        {
            var idList = itemIds.ToList();
            var query = @"UPDATE public.""ProjectItems"" SET ""Index"" = @Index WHERE ""Id"" = @Id";
            var parameters = idList.Select((id, index) => new { Id = id, Index = index + 1 });
            await _dbConnection.ExecuteAsync(query, parameters);
        }

        public async Task DeleteAsync(Guid id)
        {
            const string query = @"UPDATE public.""ProjectItems"" SET ""Status"" = 0 WHERE ""Id"" = @id";
            await _dbConnection.ExecuteAsync(query, new { id });
        }
    }
}
