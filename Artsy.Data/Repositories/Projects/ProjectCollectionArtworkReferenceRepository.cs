using Dapper;
using System.Data;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.Data.Repositories.Projects
{
    public class ProjectCollectionArtworkReferenceRepository : IProjectCollectionArtworkReferenceRepository
    {
        readonly IDbConnection _dbConnection;

        public ProjectCollectionArtworkReferenceRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<ProjectCollectionArtworkReference>> GetByCollectionAndItemIdAsync(Guid collectionId, Guid itemId)
        {
            const string query = @"
                SELECT * FROM public.""ProjectCollectionArtworkReferences""
                WHERE ""CollectionId"" = @collectionId AND ""ItemId"" = @itemId
                ORDER BY ""Created""";
            return await _dbConnection.QueryAsync<ProjectCollectionArtworkReference>(query, new { collectionId, itemId });
        }

        public async Task<IEnumerable<ProjectCollectionArtworkReference>> GetByCollectionIdAsync(Guid collectionId)
        {
            const string query = @"
                SELECT * FROM public.""ProjectCollectionArtworkReferences""
                WHERE ""CollectionId"" = @collectionId
                ORDER BY ""Created""";
            return await _dbConnection.QueryAsync<ProjectCollectionArtworkReference>(query, new { collectionId });
        }

        public async Task<ProjectCollectionArtworkReference?> GetByIdAsync(Guid id)
        {
            const string query = @"SELECT * FROM public.""ProjectCollectionArtworkReferences"" WHERE ""Id"" = @id";
            return await _dbConnection.QueryFirstOrDefaultAsync<ProjectCollectionArtworkReference>(query, new { id });
        }

        public async Task<ProjectCollectionArtworkReference> CreateAsync(ProjectCollectionArtworkReference reference)
        {
            reference.Id = Guid.NewGuid();
            const string query = @"
                INSERT INTO public.""ProjectCollectionArtworkReferences"" (""Id"", ""CollectionId"", ""ProjectId"", ""ItemId"", ""CustomImageId"", ""Created"")
                VALUES (@Id, @CollectionId, @ProjectId, @ItemId, @CustomImageId, @Created)
                RETURNING *";
            return await _dbConnection.QueryFirstAsync<ProjectCollectionArtworkReference>(query, reference);
        }

        public async Task DeleteAsync(Guid id)
        {
            const string query = @"DELETE FROM public.""ProjectCollectionArtworkReferences"" WHERE ""Id"" = @id";
            await _dbConnection.ExecuteAsync(query, new { id });
        }
    }
}
