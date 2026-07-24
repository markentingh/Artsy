using Dapper;
using System.Data;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.Data.Repositories.Projects
{
    public class ProjectImageUpscaleRepository : IProjectImageUpscaleRepository
    {
        readonly IDbConnection _dbConnection;

        public ProjectImageUpscaleRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<ProjectImageUpscale> CreateAsync(ProjectImageUpscale upscale)
        {
            upscale.Id = Guid.NewGuid();
            const string query = @"
                INSERT INTO public.""ProjectImageUpscales"" (""Id"", ""ProjectId"", ""CollectionId"", ""ItemId"", ""ArtworkId"", ""Width"", ""Height"", ""Scale"", ""Created"")
                VALUES (@Id, @ProjectId, @CollectionId, @ItemId, @ArtworkId, @Width, @Height, @Scale, @Created)
                RETURNING *";
            return await _dbConnection.QueryFirstAsync<ProjectImageUpscale>(query, upscale);
        }

        public async Task<ProjectImageUpscale?> GetByArtworkIdAsync(Guid artworkId)
        {
            const string query = @"
                SELECT * FROM public.""ProjectImageUpscales""
                WHERE ""ArtworkId"" = @artworkId";
            return await _dbConnection.QueryFirstOrDefaultAsync<ProjectImageUpscale>(query, new { artworkId });
        }

        public async Task<IEnumerable<ProjectImageUpscale>> GetByCollectionIdAsync(Guid collectionId)
        {
            const string query = @"
                SELECT * FROM public.""ProjectImageUpscales""
                WHERE ""CollectionId"" = @collectionId
                ORDER BY ""Created"" DESC";
            return await _dbConnection.QueryAsync<ProjectImageUpscale>(query, new { collectionId });
        }
    }
}
