using Dapper;
using System.Data;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.Data.Repositories.Projects
{
    public class ProjectItemBlueprintRepository : IProjectItemBlueprintRepository
    {
        readonly IDbConnection _dbConnection;

        public ProjectItemBlueprintRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<ProjectItemBlueprint>> GetByItemIdAsync(Guid itemId)
        {
            const string query = @"SELECT * FROM public.""ProjectItemBlueprint"" WHERE ""ItemId"" = @itemId ORDER BY ""Name""";
            return await _dbConnection.QueryAsync<ProjectItemBlueprint>(query, new { itemId });
        }

        public async Task<IEnumerable<ProjectItemBlueprint>> GetByItemIdsAsync(IEnumerable<Guid> itemIds)
        {
            const string query = @"SELECT * FROM public.""ProjectItemBlueprint"" WHERE ""ItemId"" = ANY(@itemIds) ORDER BY ""Name""";
            return await _dbConnection.QueryAsync<ProjectItemBlueprint>(query, new { itemIds });
        }

        public async Task<ProjectItemBlueprint?> GetByIdAsync(Guid id)
        {
            const string query = @"SELECT * FROM public.""ProjectItemBlueprint"" WHERE ""Id"" = @id";
            return await _dbConnection.QueryFirstOrDefaultAsync<ProjectItemBlueprint>(query, new { id });
        }

        public async Task<ProjectItemBlueprint> CreateAsync(ProjectItemBlueprint blueprint)
        {
            blueprint.Id = Guid.NewGuid();
            const string query = @"
                INSERT INTO public.""ProjectItemBlueprint"" (""Id"", ""ItemId"", ""ProjectId"", ""BlueprintId"", ""Name"", ""BlueprintJson"")
                VALUES (@Id, @ItemId, @ProjectId, @BlueprintId, @Name, @BlueprintJson)
                RETURNING *";
            return await _dbConnection.QueryFirstAsync<ProjectItemBlueprint>(query, blueprint);
        }

        public async Task UpdateAsync(ProjectItemBlueprint blueprint)
        {
            const string query = @"
                UPDATE public.""ProjectItemBlueprint""
                SET ""BlueprintId"" = @BlueprintId, ""Name"" = @Name, ""BlueprintJson"" = @BlueprintJson
                WHERE ""Id"" = @Id";
            await _dbConnection.ExecuteAsync(query, blueprint);
        }

        public async Task DeleteAsync(Guid id)
        {
            const string query = @"DELETE FROM public.""ProjectItemBlueprint"" WHERE ""Id"" = @id";
            await _dbConnection.ExecuteAsync(query, new { id });
        }
    }
}
