using Dapper;
using System.Data;
using System.Linq;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.Data.Repositories.Projects
{
    public class ProjectIdeaVariationRepository : IProjectIdeaVariationRepository
    {
        readonly IDbConnection _dbConnection;

        public ProjectIdeaVariationRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<ProjectIdeaVariation>> GetByIdeaIdAsync(Guid ideaId)
        {
            const string query = @"SELECT * FROM public.""ProjectIdeaVariations"" WHERE ""ProjectIdeaId"" = @ideaId ORDER BY ""Title""";
            return await _dbConnection.QueryAsync<ProjectIdeaVariation>(query, new { ideaId });
        }

        public async Task<ProjectIdeaVariation?> GetByIdAsync(Guid id)
        {
            const string query = @"SELECT * FROM public.""ProjectIdeaVariations"" WHERE ""Id"" = @id";
            return await _dbConnection.QueryFirstOrDefaultAsync<ProjectIdeaVariation>(query, new { id });
        }

        public async Task<IEnumerable<ProjectIdeaVariation>> CreateManyAsync(IEnumerable<ProjectIdeaVariation> variations)
        {
            var list = variations.ToList();
            foreach (var v in list)
            {
                v.Id = Guid.NewGuid();
            }
            const string query = @"
                INSERT INTO public.""ProjectIdeaVariations"" (""Id"", ""ProjectIdeaId"", ""Title"", ""Description"", ""IdeaJson"")
                VALUES (@Id, @ProjectIdeaId, @Title, @Description, @IdeaJson)";
            await _dbConnection.ExecuteAsync(query, list);
            return list;
        }

        public async Task DeleteAsync(Guid id)
        {
            const string query = @"DELETE FROM public.""ProjectIdeaVariations"" WHERE ""Id"" = @id";
            await _dbConnection.ExecuteAsync(query, new { id });
        }
    }
}
