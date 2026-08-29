using Dapper;
using System.Data;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.Data.Repositories.Projects
{
    public class ProjectRepository : IProjectRepository
    {
        readonly IDbConnection _dbConnection;

        public ProjectRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<Project>> GetAllAsync(Guid appUserId)
        {
            const string query = @"SELECT * FROM public.""Projects"" WHERE ""AppUserId"" = @appUserId AND ""Status"" = 1 ORDER BY ""Created"" DESC";
            return await _dbConnection.QueryAsync<Project>(query, new { appUserId });
        }

        public async Task<IEnumerable<Project>> GetArchivedAsync(Guid appUserId)
        {
            const string query = @"SELECT * FROM public.""Projects"" WHERE ""AppUserId"" = @appUserId AND ""Status"" = 0 ORDER BY ""Created"" DESC";
            return await _dbConnection.QueryAsync<Project>(query, new { appUserId });
        }

        public async Task<Project?> GetByIdAsync(Guid id, Guid appUserId)
        {
            const string query = @"SELECT * FROM public.""Projects"" WHERE ""Id"" = @id AND ""AppUserId"" = @appUserId";
            return await _dbConnection.QueryFirstOrDefaultAsync<Project>(query, new { id, appUserId });
        }

        public async Task<Project?> GetByKeyAsync(string key)
        {
            const string query = @"SELECT * FROM public.""Projects"" WHERE ""Key"" = @key";
            return await _dbConnection.QueryFirstOrDefaultAsync<Project>(query, new { key });
        }

        public async Task<Project> CreateAsync(Project project)
        {
            const string query = @"
                INSERT INTO public.""Projects"" (""Id"", ""AppUserId"", ""Title"", ""Description"", ""Key"", ""Color"", ""Status"", ""PublishToPrintify"", ""Created"")
                VALUES (@Id, @AppUserId, @Title, @Description, @Key, @Color, @Status, @PublishToPrintify, @Created)
                RETURNING *";

            project.Id = Guid.NewGuid();
            project.Created = DateTime.UtcNow;
            project.Status = 1;
            return await _dbConnection.QuerySingleAsync<Project>(query, project);
        }

        public async Task UpdateAsync(Project project)
        {
            const string query = @"
                UPDATE public.""Projects"" SET
                    ""Title"" = @Title,
                    ""Description"" = @Description,
                    ""Key"" = @Key,
                    ""Color"" = @Color,
                    ""Status"" = @Status,
                    ""PublishToPrintify"" = @PublishToPrintify
                WHERE ""Id"" = @Id AND ""AppUserId"" = @AppUserId";

            await _dbConnection.ExecuteAsync(query, project);
        }

        public async Task UpdateTitleAsync(Guid id, Guid appUserId, string title)
        {
            const string query = @"UPDATE public.""Projects"" SET ""Title"" = @title WHERE ""Id"" = @id AND ""AppUserId"" = @appUserId";
            await _dbConnection.ExecuteAsync(query, new { id, appUserId, title });
        }

        public async Task UpdateKeyAsync(Guid id, Guid appUserId, string key)
        {
            const string query = @"UPDATE public.""Projects"" SET ""Key"" = @key WHERE ""Id"" = @id AND ""AppUserId"" = @appUserId";
            await _dbConnection.ExecuteAsync(query, new { id, appUserId, key });
        }

        public async Task UpdatePublishToPrintifyAsync(Guid id, Guid appUserId, bool publishToPrintify)
        {
            const string query = @"UPDATE public.""Projects"" SET ""PublishToPrintify"" = @publishToPrintify WHERE ""Id"" = @id AND ""AppUserId"" = @appUserId";
            await _dbConnection.ExecuteAsync(query, new { id, appUserId, publishToPrintify });
        }

        public async Task UpdatePrintifyStoreIdAsync(Guid id, Guid appUserId, int? printifyStoreId)
        {
            const string query = @"UPDATE public.""Projects"" SET ""PrintifyStoreId"" = @printifyStoreId WHERE ""Id"" = @id AND ""AppUserId"" = @appUserId";
            await _dbConnection.ExecuteAsync(query, new { id, appUserId, printifyStoreId });
        }

        public async Task UpdateInstagramIdAsync(Guid id, Guid appUserId, Guid? instagramId)
        {
            const string query = @"UPDATE public.""Projects"" SET ""InstagramId"" = @instagramId WHERE ""Id"" = @id AND ""AppUserId"" = @appUserId";
            await _dbConnection.ExecuteAsync(query, new { id, appUserId, instagramId });
        }

        public async Task UpdatePostToInstagramAsync(Guid id, Guid appUserId, bool postToInstagram)
        {
            const string query = @"UPDATE public.""Projects"" SET ""PostToInstagram"" = @postToInstagram WHERE ""Id"" = @id AND ""AppUserId"" = @appUserId";
            await _dbConnection.ExecuteAsync(query, new { id, appUserId, postToInstagram });
        }

        public async Task UpdateSocialMediaConfigAsync(Guid id, Guid appUserId, string? socialMediaPrompt, string? socialMediaDescription)
        {
            const string query = @"UPDATE public.""Projects"" SET ""SocialMediaPrompt"" = @socialMediaPrompt, ""SocialMediaDescription"" = @socialMediaDescription WHERE ""Id"" = @id AND ""AppUserId"" = @appUserId";
            await _dbConnection.ExecuteAsync(query, new { id, appUserId, socialMediaPrompt, socialMediaDescription });
        }

        public async Task DeleteAsync(Guid id, Guid appUserId)
        {
            const string query = @"UPDATE public.""Projects"" SET ""Status"" = 0 WHERE ""Id"" = @id AND ""AppUserId"" = @appUserId";
            await _dbConnection.ExecuteAsync(query, new { id, appUserId });
        }

        public async Task UnarchiveAsync(Guid id, Guid appUserId)
        {
            const string query = @"UPDATE public.""Projects"" SET ""Status"" = 1 WHERE ""Id"" = @id AND ""AppUserId"" = @appUserId";
            await _dbConnection.ExecuteAsync(query, new { id, appUserId });
        }
    }
}
