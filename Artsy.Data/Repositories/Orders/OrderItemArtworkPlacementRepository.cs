using System.Data;
using Artsy.Data.Entities.Orders;
using Artsy.Data.Interfaces.Orders;
using Dapper;

namespace Artsy.Data.Repositories.Orders
{
    public class OrderItemArtworkPlacementRepository : IOrderItemArtworkPlacementRepository
    {
        private readonly IDbConnection _dbConnection;

        public OrderItemArtworkPlacementRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<OrderItemArtworkPlacement>> GetByArtworkIdAsync(Guid orderItemArtworkId)
        {
            const string query = @"SELECT * FROM public.""OrderItemArtworkPlacements""
                WHERE ""OrderItemArtworkId"" = @orderItemArtworkId ORDER BY ""Index""";
            return await _dbConnection.QueryAsync<OrderItemArtworkPlacement>(query, new { orderItemArtworkId });
        }

        public async Task<OrderItemArtworkPlacement?> GetByArtworkIdAndIndexAsync(Guid orderItemArtworkId, int index)
        {
            const string query = @"SELECT * FROM public.""OrderItemArtworkPlacements""
                WHERE ""OrderItemArtworkId"" = @orderItemArtworkId AND ""Index"" = @index";
            return await _dbConnection.QueryFirstOrDefaultAsync<OrderItemArtworkPlacement>(query, new { orderItemArtworkId, index });
        }

        public async Task<OrderItemArtworkPlacement> CreateAsync(OrderItemArtworkPlacement placement)
        {
            if (placement.Id == Guid.Empty) placement.Id = Guid.NewGuid();
            const string query = @"
                INSERT INTO public.""OrderItemArtworkPlacements"" (""Id"", ""OrderItemArtworkId"", ""Width"", ""Height"", ""Index"", ""ResponseId"", ""GroupId"", ""Position"")
                VALUES (@Id, @OrderItemArtworkId, @Width, @Height, @Index, @ResponseId, @GroupId, @Position)
                RETURNING *";
            return await _dbConnection.QueryFirstAsync<OrderItemArtworkPlacement>(query, placement);
        }

        public async Task DeleteByArtworkIdAsync(Guid orderItemArtworkId)
        {
            const string query = @"DELETE FROM public.""OrderItemArtworkPlacements""
                WHERE ""OrderItemArtworkId"" = @orderItemArtworkId";
            await _dbConnection.ExecuteAsync(query, new { orderItemArtworkId });
        }
    }
}
