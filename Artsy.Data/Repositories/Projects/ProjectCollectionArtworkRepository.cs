using Dapper;
using System.Data;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.Data.Repositories.Projects
{
    public class ProjectCollectionArtworkRepository : IProjectCollectionArtworkRepository
    {
        readonly IDbConnection _dbConnection;

        public ProjectCollectionArtworkRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<ProjectCollectionArtwork>> FilterByProjectIdAsync(Guid projectId, Guid? collectionId = null, int start = 0, int length = 5)
        {
            const string query = @"
                SELECT a.* FROM public.""ProjectCollectionArtwork"" a
                INNER JOIN public.""ProjectCollections"" c ON c.""Id"" = a.""CollectionId""
                WHERE a.""ProjectId"" = @projectId
                AND (@collectionId IS NULL OR a.""CollectionId"" = @collectionId)
                ORDER BY c.""Created"" DESC, a.""Id""
                OFFSET @start LIMIT @length";
            return await _dbConnection.QueryAsync<ProjectCollectionArtwork>(query, new { projectId, collectionId, start, length });
        }

        public async Task<IEnumerable<ProjectCollectionArtwork>> FilterByProjectIdsAsync(Guid[] projectIds, int length = 5)
        {
            const string query = @"
                WITH ranked AS (
                    SELECT a.*, ROW_NUMBER() OVER (PARTITION BY a.""ProjectId"" ORDER BY c.""Created"" DESC, a.""Id"") AS rn
                    FROM public.""ProjectCollectionArtwork"" a
                    INNER JOIN public.""ProjectCollections"" c ON c.""Id"" = a.""CollectionId""
                    WHERE a.""ProjectId"" = ANY(@projectIds)
                )
                SELECT * FROM ranked WHERE rn <= @length";
            return await _dbConnection.QueryAsync<ProjectCollectionArtwork>(query, new { projectIds, length });
        }

        public async Task<ProjectCollectionArtwork?> GetByIdAsync(Guid collectionId, Guid artworkId)
        {
            const string query = @"
                SELECT * FROM public.""ProjectCollectionArtwork""
                WHERE ""CollectionId"" = @collectionId AND ""Id"" = @artworkId";
            return await _dbConnection.QueryFirstOrDefaultAsync<ProjectCollectionArtwork>(query, new { collectionId, artworkId });
        }
    }
}
