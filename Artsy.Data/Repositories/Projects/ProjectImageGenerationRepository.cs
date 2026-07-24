using Dapper;
using System.Data;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.Data.Repositories.Projects
{
    public class ProjectImageGenerationRepository : IProjectImageGenerationRepository
    {
        readonly IDbConnection _dbConnection;

        public ProjectImageGenerationRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<ProjectImageGeneration> CreateAsync(ProjectImageGeneration generation)
        {
            generation.Id = Guid.NewGuid();
            const string query = @"
                INSERT INTO public.""ProjectImageGenerations"" (""Id"", ""ProjectId"", ""ItemId"", ""CollectionId"", ""BlueprintId"", ""InputTextTokens"", ""InputImageTokens"", ""OutputTokens"", ""ImageModel"", ""Prompt"", ""Filename"", ""HasThumbnail"", ""IsFullSize"", ""DateCreated"")
                VALUES (@Id, @ProjectId, @ItemId, @CollectionId, @BlueprintId, @InputTextTokens, @InputImageTokens, @OutputTokens, @ImageModel, @Prompt, @Filename, @HasThumbnail, @IsFullSize, @DateCreated)
                RETURNING *";
            return await _dbConnection.QueryFirstAsync<ProjectImageGeneration>(query, generation);
        }

        public async Task<ProjectImageGeneration?> GetByIdAsync(Guid id)
        {
            const string query = @"
                SELECT * FROM public.""ProjectImageGenerations""
                WHERE ""Id"" = @id";
            return await _dbConnection.QueryFirstOrDefaultAsync<ProjectImageGeneration>(query, new { id });
        }

        public async Task<IEnumerable<ProjectImageGeneration>> GetByProjectIdAsync(Guid projectId)
        {
            const string query = @"
                SELECT * FROM public.""ProjectImageGenerations""
                WHERE ""ProjectId"" = @projectId
                ORDER BY ""DateCreated"" DESC";
            return await _dbConnection.QueryAsync<ProjectImageGeneration>(query, new { projectId });
        }

        public async Task<IEnumerable<ProjectImageGeneration>> GetByCollectionIdAsync(Guid collectionId)
        {
            const string query = @"
                SELECT * FROM public.""ProjectImageGenerations""
                WHERE ""CollectionId"" = @collectionId
                ORDER BY ""DateCreated"" DESC";
            return await _dbConnection.QueryAsync<ProjectImageGeneration>(query, new { collectionId });
        }

        public async Task<IEnumerable<ProjectImageGeneration>> GetByItemIdAsync(Guid itemId)
        {
            const string query = @"
                SELECT * FROM public.""ProjectImageGenerations""
                WHERE ""ItemId"" = @itemId
                ORDER BY ""DateCreated"" DESC";
            return await _dbConnection.QueryAsync<ProjectImageGeneration>(query, new { itemId });
        }
    }
}
