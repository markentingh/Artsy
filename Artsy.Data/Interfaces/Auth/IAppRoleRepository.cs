using Artsy.Data.Entities.Auth;

namespace Artsy.Data.Interfaces.Auth
{
    public interface IAppRoleRepository
    {
        Task<IEnumerable<AppRole>> GetAll();
        Task<AppRole> GetById(int id);
        Task<AppRole> GetByName(string name);
    }
}
