using Artsy.Data.Entities;

namespace Artsy.Data.Interfaces
{
    public interface IInvoiceRepository
    {
        Task<Invoice> CreateAsync(Invoice invoice);
        Task<Invoice?> GetByIdAsync(int id);
        Task<IEnumerable<Invoice>> GetAllAsync();
        Task<IEnumerable<Invoice>> GetByAppUserIdAsync(Guid appUserId);
    }
}
