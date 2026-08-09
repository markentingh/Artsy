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
                INSERT INTO public.""ProjectImageGenerations"" (""Id"", ""ProjectId"", ""ItemId"", ""CollectionId"", ""BlueprintId"", ""AppUserId"", ""ImageGenerationId"", ""InputTextTokens"", ""InputImageTokens"", ""OutputTokens"", ""Tokens"", ""Prompt"", ""Filename"", ""Resolution"", ""InputImages"", ""InputImageJson"", ""Type"", ""Cost"", ""DateYear"", ""DateMonth"", ""DateDay"")
                VALUES (@Id, @ProjectId, @ItemId, @CollectionId, @BlueprintId, @AppUserId, @ImageGenerationId, @InputTextTokens, @InputImageTokens, @OutputTokens, @Tokens, @Prompt, @Filename, @Resolution, @InputImages, @InputImageJson, @Type, @Cost, EXTRACT(YEAR FROM NOW())::int, EXTRACT(MONTH FROM NOW())::int, EXTRACT(DAY FROM NOW())::int)
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

        public async Task<(IEnumerable<ProjectImageGeneration> items, int totalCount)> GetPaginatedAsync(int start, int length)
        {
            const string countQuery = @"SELECT COUNT(*) FROM public.""ProjectImageGenerations""";
            var totalCount = await _dbConnection.ExecuteScalarAsync<int>(countQuery);

            const string query = @"
                SELECT * FROM public.""ProjectImageGenerations""
                ORDER BY ""DateCreated"" DESC
                OFFSET @start LIMIT @length";
            var items = await _dbConnection.QueryAsync<ProjectImageGeneration>(query, new { start, length });
            return (items, totalCount);
        }

        public async Task<IEnumerable<DailyCostResult>> GetDailyCostsAsync(int days)
        {
            var endDate = DateTime.UtcNow.Date.AddDays(1);
            var startDate = endDate.AddDays(-days);

            const string query = @"
                SELECT MAKE_DATE(""DateYear"", ""DateMonth"", ""DateDay"") AS ""Date"",
                       COALESCE(SUM(""Cost""), 0) AS ""TotalCost""
                FROM public.""ProjectImageGenerations""
                WHERE ""DateCreated"" >= @startDate AND ""DateCreated"" < @endDate
                GROUP BY ""DateYear"", ""DateMonth"", ""DateDay""
                ORDER BY ""Date""";
            return await _dbConnection.QueryAsync<DailyCostResult>(query, new { startDate, endDate });
        }
    }
}
