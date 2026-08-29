using Artsy.Data.Entities.Projects;

namespace Artsy.Data.Interfaces.Projects
{
    public interface IProjectItemArtworkRepository
    {
        Task<IEnumerable<ProjectItemArtwork>> GetByProjectIdAsync(Guid projectId);
        Task<IEnumerable<ProjectItemArtwork>> GetByItemIdAsync(Guid itemId);
        Task<ProjectItemArtwork?> GetByIdAsync(Guid id);
        Task<ProjectItemArtwork> CreateAsync(ProjectItemArtwork artwork);
        Task UpdateAsync(ProjectItemArtwork artwork);
        Task UpdatePromptAsync(Guid itemId, string prompt);
        Task UpdateImageModelAsync(Guid itemId, string imageModel);
        Task UpdateArtworkTypeAsync(Guid itemId, string artworkType, Guid? customImageId);
        Task UpdateAspectRatioAsync(Guid itemId, string aspectRatio, string design);
        Task UpdateIgnoredQuestionsAsync(Guid itemId, string? ignoredQuestions);
        Task UpdateOpacityJsonAsync(Guid itemId, string? opacityJson);
        Task DeleteAsync(Guid id);
    }
}
