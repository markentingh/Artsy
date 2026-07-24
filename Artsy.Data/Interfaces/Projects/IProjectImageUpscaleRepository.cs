using Artsy.Data.Entities.Projects;

namespace Artsy.Data.Interfaces.Projects
{
    public interface IProjectImageUpscaleRepository
    {
        Task<ProjectImageUpscale> CreateAsync(ProjectImageUpscale upscale);
        Task<ProjectImageUpscale?> GetByArtworkIdAsync(Guid artworkId);
        Task<IEnumerable<ProjectImageUpscale>> GetByCollectionIdAsync(Guid collectionId);
    }
}
