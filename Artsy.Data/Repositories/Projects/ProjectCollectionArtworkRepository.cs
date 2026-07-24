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
                WHERE a.""ProjectId"" = @projectId AND c.""Status"" = 1
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
                    WHERE a.""ProjectId"" = ANY(@projectIds) AND c.""Status"" = 1
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

        public async Task<IEnumerable<ProjectCollectionArtwork>> GetByCollectionIdAsync(Guid collectionId)
        {
            const string query = @"
                SELECT a.* FROM public.""ProjectCollectionArtwork"" a
                INNER JOIN public.""ProjectCollections"" c ON c.""Id"" = a.""CollectionId""
                WHERE a.""CollectionId"" = @collectionId AND c.""Status"" = 1
                ORDER BY a.""Id""";
            return await _dbConnection.QueryAsync<ProjectCollectionArtwork>(query, new { collectionId });
        }

        public async Task<ProjectCollectionArtwork?> GetByCollectionAndItemIdAsync(Guid collectionId, Guid itemId)
        {
            const string query = @"
                SELECT * FROM public.""ProjectCollectionArtwork""
                WHERE ""CollectionId"" = @collectionId AND ""ItemId"" = @itemId";
            return await _dbConnection.QueryFirstOrDefaultAsync<ProjectCollectionArtwork>(query, new { collectionId, itemId });
        }

        public async Task<ProjectCollectionArtwork> CreateAsync(ProjectCollectionArtwork artwork)
        {
            artwork.Id = Guid.NewGuid();
            const string query = @"
                INSERT INTO public.""ProjectCollectionArtwork"" (""Id"", ""ProjectId"", ""CollectionId"", ""ItemId"", ""Active"", ""Width"", ""Height"", ""ImageModel"", ""Prompt"", ""Accepted"", ""ResponseId"", ""FullSize"")
                VALUES (@Id, @ProjectId, @CollectionId, @ItemId, @Active, @Width, @Height, @ImageModel, @Prompt, @Accepted, @ResponseId, @FullSize)
                RETURNING *";
            return await _dbConnection.QueryFirstAsync<ProjectCollectionArtwork>(query, artwork);
        }

        public async Task<ProjectCollectionArtwork> UpsertAsync(ProjectCollectionArtwork artwork)
        {
            artwork.Id = Guid.NewGuid();
            const string query = @"
                INSERT INTO public.""ProjectCollectionArtwork"" (""Id"", ""ProjectId"", ""CollectionId"", ""ItemId"", ""Active"", ""Width"", ""Height"", ""ImageModel"", ""Prompt"", ""Accepted"", ""ResponseId"", ""FullSize"")
                VALUES (@Id, @ProjectId, @CollectionId, @ItemId, @Active, @Width, @Height, @ImageModel, @Prompt, @Accepted, @ResponseId, @FullSize)
                ON CONFLICT (""CollectionId"", ""ItemId"")
                DO UPDATE SET ""Active"" = EXCLUDED.""Active"", ""Width"" = EXCLUDED.""Width"", ""Height"" = EXCLUDED.""Height"", ""ImageModel"" = EXCLUDED.""ImageModel"", ""Prompt"" = EXCLUDED.""Prompt"", ""FullSize"" = EXCLUDED.""FullSize""
                RETURNING *";
            return await _dbConnection.QueryFirstAsync<ProjectCollectionArtwork>(query, artwork);
        }

        public async Task UpdateAsync(ProjectCollectionArtwork artwork)
        {
            const string query = @"
                UPDATE public.""ProjectCollectionArtwork""
                SET ""Active"" = @Active, ""Width"" = @Width, ""Height"" = @Height,
                    ""ImageModel"" = @ImageModel, ""Prompt"" = @Prompt, ""Accepted"" = @Accepted, ""ResponseId"" = @ResponseId, ""FullSize"" = @FullSize
                WHERE ""Id"" = @Id";
            await _dbConnection.ExecuteAsync(query, artwork);
        }

        public async Task AcceptAsync(Guid collectionId, Guid itemId)
        {
            const string query = @"
                UPDATE public.""ProjectCollectionArtwork""
                SET ""Accepted"" = TRUE
                WHERE ""CollectionId"" = @collectionId AND ""ItemId"" = @itemId";
            await _dbConnection.ExecuteAsync(query, new { collectionId, itemId });
        }
    }
}
