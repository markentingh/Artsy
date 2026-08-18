using Dapper;
using System.Data;
using Artsy.Data.Entities.Orders;
using Artsy.Data.Interfaces.Orders;

namespace Artsy.Data.Repositories.Orders
{
    public class OrderItemAnswerRepository : IOrderItemAnswerRepository
    {
        readonly IDbConnection _dbConnection;

        public OrderItemAnswerRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<OrderItemAnswer>> GetByOrderItemIdAsync(Guid orderItemId)
        {
            const string query = @"
                SELECT * FROM public.""OrderItemAnswers""
                WHERE ""OrderItemId"" = @orderItemId
                ORDER BY ""Id""";
            return await _dbConnection.QueryAsync<OrderItemAnswer>(query, new { orderItemId });
        }

        public async Task UpsertAsync(OrderItemAnswer answer)
        {
            answer.Id = Guid.NewGuid();
            const string query = @"
                INSERT INTO public.""OrderItemAnswers"" (""Id"", ""OrderItemId"", ""ProjectId"", ""QuestionId"", ""ItemId"", ""Answer"")
                VALUES (@Id, @OrderItemId, @ProjectId, @QuestionId, @ItemId, @Answer)
                ON CONFLICT (""OrderItemId"", ""QuestionId"")
                DO UPDATE SET ""Answer"" = EXCLUDED.""Answer"",
                              ""ItemId"" = EXCLUDED.""ItemId"",
                              ""ProjectId"" = EXCLUDED.""ProjectId""";
            await _dbConnection.ExecuteAsync(query, answer);
        }
    }
}
