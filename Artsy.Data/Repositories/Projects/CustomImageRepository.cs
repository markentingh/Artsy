using Dapper;
using System.Data;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.Data.Repositories.Projects
{
    public class CustomImageRepository : ICustomImageRepository
    {
        readonly IDbConnection _dbConnection;

        public CustomImageRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<CustomImage>> GetByUserIdAsync(Guid appUserId, int limit = 10, int offset = 0)
        {
            const string query = @"
                SELECT * FROM public.""CustomImages""
                WHERE ""AppUserId"" = @appUserId
                ORDER BY ""Created"" DESC
                LIMIT @limit OFFSET @offset";
            return await _dbConnection.QueryAsync<CustomImage>(query, new { appUserId, limit, offset });
        }

        public async Task<CustomImage?> GetByIdAsync(Guid id)
        {
            const string query = @"SELECT * FROM public.""CustomImages"" WHERE ""Id"" = @id";
            return await _dbConnection.QueryFirstOrDefaultAsync<CustomImage>(query, new { id });
        }

        public async Task<CustomImage> CreateAsync(CustomImage image)
        {
            image.Id = Guid.NewGuid();
            image.Created = DateTime.UtcNow;
            const string query = @"
                INSERT INTO public.""CustomImages"" (""Id"", ""AppUserId"", ""FileName"", ""Extension"", ""Created"")
                VALUES (@Id, @AppUserId, @FileName, @Extension, @Created)
                RETURNING *";
            return await _dbConnection.QueryFirstAsync<CustomImage>(query, image);
        }

        public async Task DeleteAsync(Guid id)
        {
            const string query = @"DELETE FROM public.""CustomImages"" WHERE ""Id"" = @id";
            await _dbConnection.ExecuteAsync(query, new { id });
        }

        public async Task<int> CountByUserIdAsync(Guid appUserId)
        {
            const string query = @"SELECT COUNT(*) FROM public.""CustomImages"" WHERE ""AppUserId"" = @appUserId";
            return await _dbConnection.ExecuteScalarAsync<int>(query, new { appUserId });
        }
    }
}
