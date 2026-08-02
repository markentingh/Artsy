using Artsy.Data.Entities.Projects;

namespace Artsy.Data.Interfaces.Projects
{
    public interface IProjectCollectionInstagramPostRepository
    {
        Task<ProjectCollectionInstagramPost> CreateAsync(ProjectCollectionInstagramPost post);
        Task<IEnumerable<ProjectCollectionInstagramPost>> GetByCollectionIdAsync(Guid collectionId);
        Task UpdatePermalinkAsync(Guid postId, string permalink);
    }
}
