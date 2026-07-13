using Artsy.Data.Entities.Auth;

namespace Artsy.Data.Interfaces.Auth
{
    public interface IAppUserRolesRepository
    {
        Task<IEnumerable<AppUserRole>> GetByUserId(Guid userId);
        Task Add(AppUserRole userRole);
        Task Remove(Guid userId, int roleId);
    }
}
