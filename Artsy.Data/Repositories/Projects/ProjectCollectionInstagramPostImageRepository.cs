using Dapper;
using System.Data;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.Data.Repositories.Projects
{
    public class ProjectCollectionInstagramPostImageRepository : IProjectCollectionInstagramPostImageRepository
    {
        readonly IDbConnection _dbConnection;

        public ProjectCollectionInstagramPostImageRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<ProjectCollectionInstagramPostImage> CreateAsync(ProjectCollectionInstagramPostImage image)
        {
            if (image.Id == Guid.Empty)
                image.Id = Guid.NewGuid();
            image.Created = DateTime.UtcNow;
            const string query = @"
                INSERT INTO public.""ProjectCollectionInstagramPostImages"" (""Id"", ""InstagramPostId"", ""ProductImageId"", ""ArtworkId"", ""SortOrder"", ""Created"")
                VALUES (@Id, @InstagramPostId, @ProductImageId, @ArtworkId, @SortOrder, @Created)
                RETURNING *";
            return await _dbConnection.QueryFirstAsync<ProjectCollectionInstagramPostImage>(query, image);
        }

        public async Task<IEnumerable<ProjectCollectionInstagramPostImage>> GetByPostIdAsync(Guid postId)
        {
            const string query = @"SELECT * FROM public.""ProjectCollectionInstagramPostImages"" WHERE ""InstagramPostId"" = @postId ORDER BY ""SortOrder""";
            return await _dbConnection.QueryAsync<ProjectCollectionInstagramPostImage>(query, new { postId });
        }
    }
}
