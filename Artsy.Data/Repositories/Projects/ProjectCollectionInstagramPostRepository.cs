using Dapper;
using System.Data;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.Data.Repositories.Projects
{
    public class ProjectCollectionInstagramPostRepository : IProjectCollectionInstagramPostRepository
    {
        readonly IDbConnection _dbConnection;

        public ProjectCollectionInstagramPostRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<ProjectCollectionInstagramPost> CreateAsync(ProjectCollectionInstagramPost post)
        {
            if (post.Id == Guid.Empty)
                post.Id = Guid.NewGuid();
            post.Created = DateTime.UtcNow;
            const string query = @"
                INSERT INTO public.""ProjectCollectionInstagramPosts"" (""Id"", ""ProjectId"", ""CollectionId"", ""InstagramAccountId"", ""Description"", ""ContainerId"", ""Permalink"", ""Status"", ""Created"")
                VALUES (@Id, @ProjectId, @CollectionId, @InstagramAccountId, @Description, @ContainerId, @Permalink, @Status, @Created)
                RETURNING *";
            return await _dbConnection.QueryFirstAsync<ProjectCollectionInstagramPost>(query, post);
        }

        public async Task<IEnumerable<ProjectCollectionInstagramPost>> GetByCollectionIdAsync(Guid collectionId)
        {
            const string query = @"SELECT * FROM public.""ProjectCollectionInstagramPosts"" WHERE ""CollectionId"" = @collectionId AND ""Status"" = 1 ORDER BY ""Created"" DESC";
            return await _dbConnection.QueryAsync<ProjectCollectionInstagramPost>(query, new { collectionId });
        }

        public async Task UpdatePermalinkAsync(Guid postId, string permalink)
        {
            const string query = @"UPDATE public.""ProjectCollectionInstagramPosts"" SET ""Permalink"" = @permalink WHERE ""Id"" = @postId";
            await _dbConnection.ExecuteAsync(query, new { postId, permalink });
        }
    }
}
