using Dapper;
using System.Data;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.Data.Repositories.Projects
{
    public class ProjectQuestionRepository : IProjectQuestionRepository
    {
        readonly IDbConnection _dbConnection;

        public ProjectQuestionRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<ProjectQuestion>> GetByProjectIdAsync(Guid projectId)
        {
            const string query = @"SELECT * FROM public.""ProjectQuestions"" WHERE ""ProjectId"" = @projectId ORDER BY ""Index""";
            return await _dbConnection.QueryAsync<ProjectQuestion>(query, new { projectId });
        }

        public async Task<ProjectQuestion?> GetByIdAsync(Guid id)
        {
            const string query = @"SELECT * FROM public.""ProjectQuestions"" WHERE ""Id"" = @id";
            return await _dbConnection.QueryFirstOrDefaultAsync<ProjectQuestion>(query, new { id });
        }

        public async Task<ProjectQuestion> CreateAsync(ProjectQuestion question)
        {
            question.Id = Guid.NewGuid();
            const string query = @"
                INSERT INTO public.""ProjectQuestions"" (""Id"", ""ProjectId"", ""Index"", ""Question"")
                VALUES (@Id, @ProjectId, @Index, @Question)
                RETURNING *";
            return await _dbConnection.QueryFirstAsync<ProjectQuestion>(query, question);
        }

        public async Task UpdateAsync(ProjectQuestion question)
        {
            const string query = @"
                UPDATE public.""ProjectQuestions""
                SET ""Index"" = @Index, ""Question"" = @Question
                WHERE ""Id"" = @Id";
            await _dbConnection.ExecuteAsync(query, question);
        }

        public async Task DeleteAsync(Guid id)
        {
            const string query = @"DELETE FROM public.""ProjectQuestions"" WHERE ""Id"" = @id";
            await _dbConnection.ExecuteAsync(query, new { id });
        }
    }
}
