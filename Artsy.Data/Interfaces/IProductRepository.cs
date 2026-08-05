using Artsy.Data.Entities;

namespace Artsy.Data.Interfaces
{
    public interface IProductRepository
    {
        Task<Product> CreateAsync(Product product);
        Task<Product?> GetByIdAsync(int id);
        Task<IEnumerable<Product>> GetAllAsync();
        Task<IEnumerable<Product>> GetActiveAsync();
        Task UpdateAsync(Product product);
        Task ArchiveAsync(int id);
    }
}
