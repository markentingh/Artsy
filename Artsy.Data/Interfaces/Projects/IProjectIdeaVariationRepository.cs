using Artsy.Data.Entities.Projects;

namespace Artsy.Data.Interfaces.Projects
{
    public interface IProjectIdeaVariationRepository
    {
        Task<IEnumerable<ProjectIdeaVariation>> GetByIdeaIdAsync(Guid ideaId);
        Task<ProjectIdeaVariation?> GetByIdAsync(Guid id);
        Task<IEnumerable<ProjectIdeaVariation>> CreateManyAsync(IEnumerable<ProjectIdeaVariation> variations);
        Task DeleteAsync(Guid id);
    }
}
