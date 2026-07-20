using Dapper;
using System.Data;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.Data.Repositories.Projects
{
    public class ProjectItemQuestionRepository : IProjectItemQuestionRepository
    {
        readonly IDbConnection _dbConnection;

        public ProjectItemQuestionRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<ProjectItemQuestion>> GetByItemIdAsync(Guid itemId)
        {
            const string query = @"SELECT * FROM public.""ProjectItemQuestions"" WHERE ""ItemId"" = @itemId ORDER BY ""Index""";
            return await _dbConnection.QueryAsync<ProjectItemQuestion>(query, new { itemId });
        }

        public async Task<IEnumerable<ProjectItemQuestion>> GetByProjectIdAsync(Guid projectId)
        {
            const string query = @"SELECT * FROM public.""ProjectItemQuestions"" WHERE ""ProjectId"" = @projectId ORDER BY ""Index""";
            return await _dbConnection.QueryAsync<ProjectItemQuestion>(query, new { projectId });
        }

        public async Task<ProjectItemQuestion?> GetByIdAsync(Guid id)
        {
            const string query = @"SELECT * FROM public.""ProjectItemQuestions"" WHERE ""Id"" = @id";
            return await _dbConnection.QueryFirstOrDefaultAsync<ProjectItemQuestion>(query, new { id });
        }

        public async Task<ProjectItemQuestion> CreateAsync(ProjectItemQuestion question)
        {
            question.Id = Guid.NewGuid();
            const string query = @"
                INSERT INTO public.""ProjectItemQuestions"" (""Id"", ""ItemId"", ""ProjectId"", ""Index"", ""Question"")
                VALUES (@Id, @ItemId, @ProjectId, @Index, @Question)
                RETURNING *";
            return await _dbConnection.QueryFirstAsync<ProjectItemQuestion>(query, question);
        }

        public async Task UpdateAsync(ProjectItemQuestion question)
        {
            const string query = @"
                UPDATE public.""ProjectItemQuestions""
                SET ""Index"" = @Index, ""Question"" = @Question
                WHERE ""Id"" = @Id";
            await _dbConnection.ExecuteAsync(query, question);
        }

        public async Task DeleteAsync(Guid id)
        {
            const string query = @"DELETE FROM public.""ProjectItemQuestions"" WHERE ""Id"" = @id";
            await _dbConnection.ExecuteAsync(query, new { id });
        }
    }
}
