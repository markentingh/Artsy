using Dapper;
using System.Data;
using Artsy.Data.Entities.Orders;
using Artsy.Data.Interfaces.Orders;

namespace Artsy.Data.Repositories.Orders
{
    public class HangfireOrderRepository : IHangfireOrderRepository
    {
        readonly IDbConnection _dbConnection;

        public HangfireOrderRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public void Dispose()
        {
            _dbConnection?.Dispose();
        }

        public async Task<HangfireOrder?> GetLatestAsync()
        {
            const string query = @"SELECT * FROM public.""HangfireOrders"" ORDER BY ""DateChecked"" DESC LIMIT 1";
            return await _dbConnection.QueryFirstOrDefaultAsync<HangfireOrder>(query);
        }

        public async Task<IEnumerable<HangfireOrder>> GetByDateRangeAsync(DateTime since)
        {
            const string query = @"SELECT * FROM public.""HangfireOrders"" WHERE ""DateChecked"" >= @since ORDER BY ""DateChecked""";
            return await _dbConnection.QueryAsync<HangfireOrder>(query, new { since });
        }

        public async Task AddAsync(HangfireOrder record)
        {
            record.Id = Guid.NewGuid();
            record.DateChecked = DateTime.UtcNow;
            const string query = @"
                INSERT INTO public.""HangfireOrders"" (""Id"", ""DateChecked"", ""NewOrders"", ""UpdatedOrders"")
                VALUES (@Id, @DateChecked, @NewOrders, @UpdatedOrders)";
            await _dbConnection.ExecuteAsync(query, record);
        }
    }
}
