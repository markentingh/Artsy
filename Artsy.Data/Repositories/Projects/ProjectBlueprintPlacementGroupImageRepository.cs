using System.Data;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Projects;
using Dapper;

namespace Artsy.Data.Repositories.Projects
{
    public class ProjectBlueprintPlacementGroupImageRepository : IProjectBlueprintPlacementGroupImageRepository
    {
        private readonly IDbConnection _dbConnection;

        public ProjectBlueprintPlacementGroupImageRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<ProjectBlueprintPlacementGroupImage>> GetByGroupIdAsync(Guid groupId)
        {
            const string query = @"SELECT * FROM public.""ProjectBlueprintPlacementGroupImages""
                WHERE ""GroupId"" = @groupId ORDER BY ""Index""";
            return await _dbConnection.QueryAsync<ProjectBlueprintPlacementGroupImage>(query, new { groupId });
        }

        public async Task<IEnumerable<ProjectBlueprintPlacementGroupImage>> GetByProjectAndBlueprintAsync(Guid projectId, int blueprintId)
        {
            const string query = @"SELECT * FROM public.""ProjectBlueprintPlacementGroupImages""
                WHERE ""ProjectId"" = @projectId AND ""BlueprintId"" = @blueprintId ORDER BY ""GroupId"", ""Index""";
            return await _dbConnection.QueryAsync<ProjectBlueprintPlacementGroupImage>(query, new { projectId, blueprintId });
        }

        public async Task<ProjectBlueprintPlacementGroupImage> CreateAsync(ProjectBlueprintPlacementGroupImage image)
        {
            if (image.Id == Guid.Empty) image.Id = Guid.NewGuid();
            const string query = @"
                INSERT INTO public.""ProjectBlueprintPlacementGroupImages"" (""Id"", ""ProjectId"", ""BlueprintId"", ""GroupId"", ""Index"", ""ArtworkId"", ""CustomId"", ""Position"", ""FlipX"", ""FlipY"")
                VALUES (@Id, @ProjectId, @BlueprintId, @GroupId, @Index, @ArtworkId, @CustomId, @Position, @FlipX, @FlipY)
                RETURNING *";
            return await _dbConnection.QueryFirstAsync<ProjectBlueprintPlacementGroupImage>(query, image);
        }

        public async Task UpdateAsync(ProjectBlueprintPlacementGroupImage image)
        {
            const string query = @"
                UPDATE public.""ProjectBlueprintPlacementGroupImages""
                SET ""Index"" = @Index, ""ArtworkId"" = @ArtworkId, ""CustomId"" = @CustomId, ""Position"" = @Position, ""FlipX"" = @FlipX, ""FlipY"" = @FlipY
                WHERE ""Id"" = @Id";
            await _dbConnection.ExecuteAsync(query, image);
        }

        public async Task DeleteAsync(Guid id)
        {
            const string query = @"DELETE FROM public.""ProjectBlueprintPlacementGroupImages"" WHERE ""Id"" = @id";
            await _dbConnection.ExecuteAsync(query, new { id });
        }

        public async Task DeleteByGroupIdAsync(Guid groupId)
        {
            const string query = @"DELETE FROM public.""ProjectBlueprintPlacementGroupImages"" WHERE ""GroupId"" = @groupId";
            await _dbConnection.ExecuteAsync(query, new { groupId });
        }
    }
}
