using System.Data;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Projects;
using Dapper;

namespace Artsy.Data.Repositories.Projects
{
    public class ProjectBlueprintPlacementGroupRepository : IProjectBlueprintPlacementGroupRepository
    {
        private readonly IDbConnection _dbConnection;

        public ProjectBlueprintPlacementGroupRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<ProjectBlueprintPlacementGroup>> GetByProjectAndBlueprintAsync(Guid projectId, int blueprintId)
        {
            const string query = @"SELECT * FROM public.""ProjectBlueprintPlacementGroups""
                WHERE ""ProjectId"" = @projectId AND ""BlueprintId"" = @blueprintId";
            return await _dbConnection.QueryAsync<ProjectBlueprintPlacementGroup>(query, new { projectId, blueprintId });
        }

        public async Task<ProjectBlueprintPlacementGroup> CreateAsync(ProjectBlueprintPlacementGroup group)
        {
            if (group.Id == Guid.Empty) group.Id = Guid.NewGuid();
            const string query = @"
                INSERT INTO public.""ProjectBlueprintPlacementGroups"" (""Id"", ""ProjectId"", ""BlueprintId"")
                VALUES (@Id, @ProjectId, @BlueprintId)
                RETURNING *";
            return await _dbConnection.QueryFirstAsync<ProjectBlueprintPlacementGroup>(query, group);
        }

        public async Task DeleteAsync(Guid id)
        {
            const string query = @"DELETE FROM public.""ProjectBlueprintPlacementGroups"" WHERE ""Id"" = @id";
            await _dbConnection.ExecuteAsync(query, new { id });
        }
    }
}
