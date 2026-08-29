using Dapper;
using System.Data;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.Data.Repositories.Projects
{
    public class ProjectItemArtworkRepository : IProjectItemArtworkRepository
    {
        readonly IDbConnection _dbConnection;

        public ProjectItemArtworkRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<ProjectItemArtwork>> GetByProjectIdAsync(Guid projectId)
        {
            const string query = @"
                SELECT a.* FROM public.""ProjectItemArtwork"" a
                INNER JOIN public.""ProjectItems"" i ON i.""Id"" = a.""ItemId""
                WHERE a.""ProjectId"" = @projectId AND i.""Status"" = 1
                ORDER BY a.""ItemId""";
            return await _dbConnection.QueryAsync<ProjectItemArtwork>(query, new { projectId });
        }

        public async Task<IEnumerable<ProjectItemArtwork>> GetByItemIdAsync(Guid itemId)
        {
            const string query = @"
                SELECT a.* FROM public.""ProjectItemArtwork"" a
                INNER JOIN public.""ProjectItems"" i ON i.""Id"" = a.""ItemId""
                WHERE a.""ItemId"" = @itemId AND i.""Status"" = 1";
            return await _dbConnection.QueryAsync<ProjectItemArtwork>(query, new { itemId });
        }

        public async Task<ProjectItemArtwork?> GetByIdAsync(Guid id)
        {
            const string query = @"SELECT * FROM public.""ProjectItemArtwork"" WHERE ""Id"" = @id";
            return await _dbConnection.QueryFirstOrDefaultAsync<ProjectItemArtwork>(query, new { id });
        }

        public async Task<ProjectItemArtwork> CreateAsync(ProjectItemArtwork artwork)
        {
            artwork.Id = Guid.NewGuid();
            const string query = @"
                INSERT INTO public.""ProjectItemArtwork"" (""Id"", ""ItemId"", ""ProjectId"", ""ImageModel"", ""Prompt"", ""ArtworkType"", ""CustomImageId"", ""IgnoredQuestions"", ""OpacityJson"", ""AspectRatio"", ""Design"")
                VALUES (@Id, @ItemId, @ProjectId, @ImageModel, @Prompt, @ArtworkType, @CustomImageId, @IgnoredQuestions, @OpacityJson, @AspectRatio, @Design)
                RETURNING *";
            return await _dbConnection.QueryFirstAsync<ProjectItemArtwork>(query, artwork);
        }

        public async Task UpdateAsync(ProjectItemArtwork artwork)
        {
            const string query = @"
                UPDATE public.""ProjectItemArtwork""
                SET ""ImageModel"" = @ImageModel, ""Prompt"" = @Prompt, ""ArtworkType"" = @ArtworkType, ""CustomImageId"" = @CustomImageId, ""IgnoredQuestions"" = @IgnoredQuestions, ""OpacityJson"" = @OpacityJson, ""AspectRatio"" = @AspectRatio, ""Design"" = @Design
                WHERE ""Id"" = @Id";
            await _dbConnection.ExecuteAsync(query, artwork);
        }

        public async Task UpdatePromptAsync(Guid itemId, string prompt)
        {
            const string query = @"
        UPDATE public.""ProjectItemArtwork""
        SET ""Prompt"" = @prompt
        WHERE ""ItemId"" = @itemId";
            await _dbConnection.ExecuteAsync(query, new { itemId, prompt });
        }

        public async Task UpdateImageModelAsync(Guid itemId, string imageModel)
        {
            const string query = @"
        UPDATE public.""ProjectItemArtwork""
        SET ""ImageModel"" = @imageModel
        WHERE ""ItemId"" = @itemId";
            await _dbConnection.ExecuteAsync(query, new { itemId, imageModel });
        }

        public async Task UpdateArtworkTypeAsync(Guid itemId, string artworkType, Guid? customImageId)
        {
            const string query = @"
        UPDATE public.""ProjectItemArtwork""
        SET ""ArtworkType"" = @artworkType, ""CustomImageId"" = @customImageId
        WHERE ""ItemId"" = @itemId";
            await _dbConnection.ExecuteAsync(query, new { itemId, artworkType, customImageId });
        }

        public async Task UpdateAspectRatioAsync(Guid itemId, string aspectRatio, string design)
        {
            const string query = @"
        UPDATE public.""ProjectItemArtwork""
        SET ""AspectRatio"" = @aspectRatio, ""Design"" = @design
        WHERE ""ItemId"" = @itemId";
            await _dbConnection.ExecuteAsync(query, new { itemId, aspectRatio, design });
        }

        public async Task UpdateIgnoredQuestionsAsync(Guid itemId, string? ignoredQuestions)
        {
            const string query = @"
        UPDATE public.""ProjectItemArtwork""
        SET ""IgnoredQuestions"" = @ignoredQuestions
        WHERE ""ItemId"" = @itemId";
            await _dbConnection.ExecuteAsync(query, new { itemId, ignoredQuestions });
        }

        public async Task UpdateOpacityJsonAsync(Guid itemId, string? opacityJson)
        {
            const string query = @"
        UPDATE public.""ProjectItemArtwork""
        SET ""OpacityJson"" = @opacityJson
        WHERE ""ItemId"" = @itemId";
            await _dbConnection.ExecuteAsync(query, new { itemId, opacityJson });
        }

        public async Task DeleteAsync(Guid id)
        {
            const string query = @"DELETE FROM public.""ProjectItemArtwork"" WHERE ""Id"" = @id";
            await _dbConnection.ExecuteAsync(query, new { id });
        }
    }
}
