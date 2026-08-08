using Dapper;
using System.Data;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.Data.Repositories.Projects
{
    public class ProjectIdeaRepository : IProjectIdeaRepository
    {
        readonly IDbConnection _dbConnection;

        public ProjectIdeaRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<ProjectIdea>> GetByProjectIdAsync(Guid projectId)
        {
            const string query = @"SELECT * FROM public.""ProjectIdeas"" WHERE ""ProjectId"" = @projectId ORDER BY ""Created"" DESC";
            return await _dbConnection.QueryAsync<ProjectIdea>(query, new { projectId });
        }

        public async Task<ProjectIdea?> GetByIdAsync(Guid id)
        {
            const string query = @"SELECT * FROM public.""ProjectIdeas"" WHERE ""Id"" = @id";
            return await _dbConnection.QueryFirstOrDefaultAsync<ProjectIdea>(query, new { id });
        }

        public async Task<ProjectIdea> CreateAsync(ProjectIdea idea)
        {
            idea.Id = Guid.NewGuid();
            idea.Created = DateTime.UtcNow;
            const string query = @"
                INSERT INTO public.""ProjectIdeas"" (""Id"", ""ProjectId"", ""Title"", ""Prompt"", ""Created"")
                VALUES (@Id, @ProjectId, @Title, @Prompt, @Created)
                RETURNING *";
            return await _dbConnection.QueryFirstAsync<ProjectIdea>(query, idea);
        }

        public async Task DeleteAsync(Guid id)
        {
            const string deleteVariationsQuery = @"DELETE FROM public.""ProjectIdeaVariations"" WHERE ""ProjectIdeaId"" = @id";
            const string deleteIdeaQuery = @"DELETE FROM public.""ProjectIdeas"" WHERE ""Id"" = @id";
            await _dbConnection.ExecuteAsync(deleteVariationsQuery, new { id });
            await _dbConnection.ExecuteAsync(deleteIdeaQuery, new { id });
        }
    }
}
