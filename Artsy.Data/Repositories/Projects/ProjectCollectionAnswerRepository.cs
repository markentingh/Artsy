using Dapper;
using System.Data;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.Data.Repositories.Projects
{
    public class ProjectCollectionAnswerRepository : IProjectCollectionAnswerRepository
    {
        readonly IDbConnection _dbConnection;

        public ProjectCollectionAnswerRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<ProjectCollectionAnswer>> GetByCollectionIdAsync(Guid collectionId)
        {
            const string query = @"
                SELECT * FROM public.""ProjectCollectionAnswers""
                WHERE ""CollectionId"" = @collectionId
                ORDER BY ""Id""";
            return await _dbConnection.QueryAsync<ProjectCollectionAnswer>(query, new { collectionId });
        }

        public async Task<ProjectCollectionAnswer> CreateAsync(ProjectCollectionAnswer answer)
        {
            answer.Id = Guid.NewGuid();
            const string query = @"
                INSERT INTO public.""ProjectCollectionAnswers"" (""Id"", ""ProjectId"", ""CollectionId"", ""QuestionId"", ""ItemId"", ""Answer"")
                VALUES (@Id, @ProjectId, @CollectionId, @QuestionId, @ItemId, @Answer)
                RETURNING *";
            return await _dbConnection.QueryFirstAsync<ProjectCollectionAnswer>(query, answer);
        }

        public async Task UpsertAsync(ProjectCollectionAnswer answer)
        {
            answer.Id = Guid.NewGuid();
            const string query = @"
                INSERT INTO public.""ProjectCollectionAnswers"" (""Id"", ""ProjectId"", ""CollectionId"", ""QuestionId"", ""ItemId"", ""Answer"")
                VALUES (@Id, @ProjectId, @CollectionId, @QuestionId, @ItemId, @Answer)
                ON CONFLICT (""CollectionId"", ""QuestionId"", ""ItemId"")
                DO UPDATE SET ""Answer"" = EXCLUDED.""Answer""";
            await _dbConnection.ExecuteAsync(query, answer);
        }
    }
}
