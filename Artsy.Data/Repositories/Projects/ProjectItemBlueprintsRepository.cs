using Dapper;
using System.Data;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.Data.Repositories.Projects
{
    public class ProjectBlueprintsRepository : IProjectBlueprintsRepository
    {
        readonly IDbConnection _dbConnection;

        public ProjectBlueprintsRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<ProjectBlueprints>> GetByProjectIdAsync(Guid projectId)
        {
            const string query = @"SELECT * FROM public.""ProjectBlueprints"" WHERE ""ProjectId"" = @projectId ORDER BY ""Name""";
            return await _dbConnection.QueryAsync<ProjectBlueprints>(query, new { projectId });
        }

        public async Task<IEnumerable<ProjectBlueprintListDto>> GetListByProjectIdAsync(Guid projectId)
        {
            const string query = @"
                SELECT b.""Id"", b.""BlueprintId"", b.""Name"", b.""BlueprintJson"", b.""PlacementJson"",
                    COALESCE(p.""ImageCount"", 0) AS ""ImageCount""
                FROM public.""ProjectBlueprints"" b
                LEFT JOIN public.""PrintifyBlueprints"" p ON p.""BlueprintId"" = b.""BlueprintId""
                WHERE b.""ProjectId"" = @projectId
                ORDER BY b.""Name""";
            return await _dbConnection.QueryAsync<ProjectBlueprintListDto>(query, new { projectId });
        }

        public async Task<ProjectBlueprints?> GetByIdAsync(Guid id)
        {
            const string query = @"SELECT * FROM public.""ProjectBlueprints"" WHERE ""Id"" = @id";
            return await _dbConnection.QueryFirstOrDefaultAsync<ProjectBlueprints>(query, new { id });
        }

        public async Task<ProjectBlueprints> CreateAsync(ProjectBlueprints blueprint)
        {
            blueprint.Id = Guid.NewGuid();
            const string query = @"
                INSERT INTO public.""ProjectBlueprints"" (""Id"", ""ProjectId"", ""BlueprintId"", ""Name"", ""BlueprintJson"", ""PlacementJson"")
                VALUES (@Id, @ProjectId, @BlueprintId, @Name, @BlueprintJson, @PlacementJson)
                RETURNING *";
            return await _dbConnection.QueryFirstAsync<ProjectBlueprints>(query, blueprint);
        }

        public async Task UpdateAsync(ProjectBlueprints blueprint)
        {
            const string query = @"
                UPDATE public.""ProjectBlueprints""
                SET ""BlueprintId"" = @BlueprintId, ""Name"" = @Name, ""BlueprintJson"" = @BlueprintJson, ""PlacementJson"" = @PlacementJson
                WHERE ""Id"" = @Id";
            await _dbConnection.ExecuteAsync(query, blueprint);
        }

        public async Task UpdatePlacementAsync(Guid id, string placementJson)
        {
            const string query = @"UPDATE public.""ProjectBlueprints"" SET ""PlacementJson"" = @placementJson WHERE ""Id"" = @id";
            await _dbConnection.ExecuteAsync(query, new { id, placementJson });
        }

        public async Task DeleteAsync(Guid id)
        {
            const string query = @"DELETE FROM public.""ProjectBlueprints"" WHERE ""Id"" = @id";
            await _dbConnection.ExecuteAsync(query, new { id });
        }
    }
}
