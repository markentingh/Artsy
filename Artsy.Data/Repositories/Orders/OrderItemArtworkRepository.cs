using Dapper;
using System.Data;
using Artsy.Data.Entities.Orders;
using Artsy.Data.Interfaces.Orders;

namespace Artsy.Data.Repositories.Orders
{
    public class OrderItemArtworkRepository : IOrderItemArtworkRepository
    {
        readonly IDbConnection _dbConnection;

        public OrderItemArtworkRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<OrderItemArtwork>> GetByOrderItemIdAsync(Guid orderItemId)
        {
            const string query = @"SELECT * FROM public.""OrderItemArtworks"" WHERE ""OrderItemId"" = @orderItemId AND ""Active"" = TRUE ORDER BY ""Index""";
            return await _dbConnection.QueryAsync<OrderItemArtwork>(query, new { orderItemId });
        }

        public async Task<OrderItemArtwork?> GetByIdAsync(Guid id)
        {
            const string query = @"SELECT * FROM public.""OrderItemArtworks"" WHERE ""Id"" = @id";
            return await _dbConnection.QueryFirstOrDefaultAsync<OrderItemArtwork>(query, new { id });
        }

        public async Task<OrderItemArtwork> CreateAsync(OrderItemArtwork artwork)
        {
            artwork.Id = Guid.NewGuid();
            artwork.Created = DateTime.UtcNow;
            artwork.Updated = DateTime.UtcNow;
            const string query = @"
                INSERT INTO public.""OrderItemArtworks"" (""Id"", ""OrderId"", ""OrderItemId"", ""ProjectId"", ""CollectionId"", ""ItemId"", ""Active"", ""Width"", ""Height"", ""ImageModel"", ""Prompt"", ""Accepted"", ""ResponseId"", ""FullSize"", ""Index"", ""PrintifyImageId"", ""Opacity"", ""RequestText"", ""PlacementIndex"", ""TotalPlacements"", ""Created"", ""Updated"")
                VALUES (@Id, @OrderId, @OrderItemId, @ProjectId, @CollectionId, @ItemId, @Active, @Width, @Height, @ImageModel, @Prompt, @Accepted, @ResponseId, @FullSize, @Index, @PrintifyImageId, @Opacity, @RequestText, @PlacementIndex, @TotalPlacements, @Created, @Updated)
                RETURNING *";
            return await _dbConnection.QueryFirstAsync<OrderItemArtwork>(query, artwork);
        }

        public async Task UpdateAsync(OrderItemArtwork artwork)
        {
            const string query = @"
                UPDATE public.""OrderItemArtworks"" SET
                    ""OrderId"" = @OrderId,
                    ""OrderItemId"" = @OrderItemId,
                    ""ProjectId"" = @ProjectId,
                    ""CollectionId"" = @CollectionId,
                    ""ItemId"" = @ItemId,
                    ""Active"" = @Active,
                    ""Width"" = @Width,
                    ""Height"" = @Height,
                    ""ImageModel"" = @ImageModel,
                    ""Prompt"" = @Prompt,
                    ""Accepted"" = @Accepted,
                    ""ResponseId"" = @ResponseId,
                    ""FullSize"" = @FullSize,
                    ""Index"" = @Index,
                    ""PrintifyImageId"" = @PrintifyImageId,
                    ""Opacity"" = @Opacity,
                    ""RequestText"" = @RequestText,
                    ""PlacementIndex"" = @PlacementIndex,
                    ""TotalPlacements"" = @TotalPlacements,
                    ""Updated"" = @Updated
                WHERE ""Id"" = @Id";
            await _dbConnection.ExecuteAsync(query, artwork);
        }

        public async Task DeleteAsync(Guid id)
        {
            const string query = @"UPDATE public.""OrderItemArtworks"" SET ""Active"" = FALSE WHERE ""Id"" = @id";
            await _dbConnection.ExecuteAsync(query, new { id });
        }
    }
}
