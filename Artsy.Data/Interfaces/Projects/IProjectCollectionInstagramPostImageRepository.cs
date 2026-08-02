using Artsy.Data.Entities.Projects;

namespace Artsy.Data.Interfaces.Projects
{
    public interface IProjectCollectionInstagramPostImageRepository
    {
        Task<ProjectCollectionInstagramPostImage> CreateAsync(ProjectCollectionInstagramPostImage image);
        Task<IEnumerable<ProjectCollectionInstagramPostImage>> GetByPostIdAsync(Guid postId);
    }
}
