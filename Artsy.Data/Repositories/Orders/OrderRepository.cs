using Dapper;
using System.Data;
using Artsy.Data.Entities.Orders;
using Artsy.Data.Interfaces.Orders;
using Artsy.Data.Models;

namespace Artsy.Data.Repositories.Orders
{
    public class OrderRepository : IOrderRepository
    {
        readonly IDbConnection _dbConnection;

        public OrderRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public void Dispose()
        {
            _dbConnection?.Dispose();
        }

        public async Task<IEnumerable<Order>> GetByUserAsync(Guid appUserId)
        {
            const string query = @"SELECT * FROM public.""Orders"" WHERE ""AppUserId"" = @appUserId ORDER BY ""DateCreated"" DESC";
            return await _dbConnection.QueryAsync<Order>(query, new { appUserId });
        }

        public async Task<IEnumerable<OrderWithDetails>> GetByUserWithDetailsAsync(Guid appUserId)
        {
            const string ordersSql = @"SELECT * FROM public.""Orders"" WHERE ""AppUserId"" = @appUserId ORDER BY ""DateCreated"" DESC";
            const string itemsSql = @"SELECT i.* FROM public.""OrderItems"" i JOIN public.""Orders"" o ON i.""OrderId"" = o.""Id"" WHERE o.""AppUserId"" = @appUserId";
            const string shipmentsSql = @"SELECT s.* FROM public.""OrderShipments"" s JOIN public.""Orders"" o ON s.""OrderId"" = o.""Id"" WHERE o.""AppUserId"" = @appUserId";

            var orders = (await _dbConnection.QueryAsync<Order>(ordersSql, new { appUserId })).ToList();
            var items = (await _dbConnection.QueryAsync<OrderItem>(itemsSql, new { appUserId })).AsList();
            var shipments = (await _dbConnection.QueryAsync<OrderShipment>(shipmentsSql, new { appUserId })).AsList();

            var orderDict = orders.ToDictionary(o => o.Id, o => new OrderWithDetails { Order = o });
            foreach (var item in items)
                if (orderDict.TryGetValue(item.OrderId, out var details)) details.Items.Add(item);
            foreach (var ship in shipments)
                if (orderDict.TryGetValue(ship.OrderId, out var details)) details.Shipments.Add(ship);

            return orderDict.Values;
        }

        public async Task<Order?> GetByOrderIdAsync(string orderId)
        {
            const string query = @"SELECT * FROM public.""Orders"" WHERE ""OrderId"" = @orderId";
            return await _dbConnection.QueryFirstOrDefaultAsync<Order>(query, new { orderId });
        }

        public async Task<OrderWithDetails?> GetByIdWithDetailsAsync(Guid id)
        {
            const string orderQuery = @"SELECT * FROM public.""Orders"" WHERE ""Id"" = @id";
            const string itemsQuery = @"SELECT * FROM public.""OrderItems"" WHERE ""OrderId"" = @id";
            const string shipmentsQuery = @"SELECT * FROM public.""OrderShipments"" WHERE ""OrderId"" = @id";

            var order = await _dbConnection.QueryFirstOrDefaultAsync<Order>(orderQuery, new { id });
            if (order == null) return null;

            var items = (await _dbConnection.QueryAsync<OrderItem>(itemsQuery, new { id })).AsList();
            var shipments = (await _dbConnection.QueryAsync<OrderShipment>(shipmentsQuery, new { id })).AsList();

            return new OrderWithDetails { Order = order, Items = items, Shipments = shipments };
        }

        public async Task<IEnumerable<PrintifyShopWithUser>> GetDistinctActiveShopsAsync()
        {
            const string query = @"
                SELECT DISTINCT p.""AppUserId"" AS AppUserId, p.""PrintifyStoreId"" AS PrintifyShopId
                FROM public.""Projects"" p
                WHERE p.""PublishToPrintify"" = TRUE
                  AND p.""PrintifyStoreId"" IS NOT NULL
                  AND p.""Status"" = 1
                  AND EXISTS (
                      SELECT 1 FROM public.""ProjectCollectionPrintifyProducts"" pcpp
                      WHERE pcpp.""ProjectId"" = p.""Id"" AND pcpp.""Published"" = TRUE AND pcpp.""Status"" = 1
                  )";
            return await _dbConnection.QueryAsync<PrintifyShopWithUser>(query);
        }

        public async Task<SyncResultItem> SyncOrderAsync(Order order, List<OrderItem> items, List<OrderShipment> shipments, string dataHash)
        {
            var existing = await GetByOrderIdAsync(order.OrderId);
            if (existing != null && existing.DataHash == dataHash)
                return new SyncResultItem();

            if (existing == null)
            {
                order.Id = Guid.NewGuid();
                order.DataHash = dataHash;
                order.Created = DateTime.UtcNow;
                order.Updated = DateTime.UtcNow;

                const string insert = @"
                    INSERT INTO public.""Orders"" (""Id"", ""AppUserId"", ""PrintifyShopId"", ""OrderId"", ""AppOrderId"", ""AddressTo"", ""Metadata"", ""TotalPrice"", ""TotalShipping"", ""TotalTax"", ""Status"", ""ShippingMethod"", ""IsExpress"", ""IsEconomyShipping"", ""DateCreated"", ""DateSentToProduction"", ""DateFulfilled"", ""PrintifyConnect"", ""DataHash"", ""ResponseJson"", ""Created"", ""Updated"")
                    VALUES (@Id, @AppUserId, @PrintifyShopId, @OrderId, @AppOrderId, @AddressTo, @Metadata, @TotalPrice, @TotalShipping, @TotalTax, @Status, @ShippingMethod, @IsExpress, @IsEconomyShipping, @DateCreated, @DateSentToProduction, @DateFulfilled, @PrintifyConnect, @DataHash, @ResponseJson, @Created, @Updated)";
                await _dbConnection.ExecuteAsync(insert, order);
                await InsertItemsAndShipmentsAsync(order.Id, items, shipments);
                return new SyncResultItem { IsNew = true };
            }
            else
            {
                order.Id = existing.Id;
                order.DataHash = dataHash;
                order.Created = existing.Created;
                order.Updated = DateTime.UtcNow;

                const string update = @"
                    UPDATE public.""Orders"" SET
                        ""AppUserId"" = @AppUserId,
                        ""PrintifyShopId"" = @PrintifyShopId,
                        ""AppOrderId"" = @AppOrderId,
                        ""AddressTo"" = @AddressTo,
                        ""Metadata"" = @Metadata,
                        ""TotalPrice"" = @TotalPrice,
                        ""TotalShipping"" = @TotalShipping,
                        ""TotalTax"" = @TotalTax,
                        ""Status"" = @Status,
                        ""ShippingMethod"" = @ShippingMethod,
                        ""IsExpress"" = @IsExpress,
                        ""IsEconomyShipping"" = @IsEconomyShipping,
                        ""DateCreated"" = @DateCreated,
                        ""DateSentToProduction"" = @DateSentToProduction,
                        ""DateFulfilled"" = @DateFulfilled,
                        ""PrintifyConnect"" = @PrintifyConnect,
                        ""DataHash"" = @DataHash,
                        ""ResponseJson"" = @ResponseJson,
                        ""Updated"" = @Updated
                    WHERE ""Id"" = @Id";
                await _dbConnection.ExecuteAsync(update, order);

                const string selectItems = @"SELECT * FROM public.""OrderItems"" WHERE ""OrderId"" = @id";
                var existingItems = (await _dbConnection.QueryAsync<OrderItem>(selectItems, new { id = order.Id })).AsList();
                var existingItemMap = new Dictionary<string, OrderItem>();
                foreach (var ei in existingItems)
                {
                    var key = $"{ei.ProductId}|{ei.VariantId}";
                    if (!existingItemMap.ContainsKey(key))
                        existingItemMap[key] = ei;
                }

                const string updateItem = @"
                    UPDATE public.""OrderItems"" SET
                        ""ProductId"" = @ProductId,
                        ""Quantity"" = @Quantity,
                        ""VariantId"" = @VariantId,
                        ""PrintProviderId"" = @PrintProviderId,
                        ""Cost"" = @Cost,
                        ""ShippingCost"" = @ShippingCost,
                        ""Status"" = @Status,
                        ""Metadata"" = @Metadata,
                        ""DateSentToProduction"" = @DateSentToProduction,
                        ""DateFulfilled"" = @DateFulfilled,
                        ""ProjectId"" = @ProjectId,
                        ""CollectionId"" = @CollectionId,
                        ""CollectionProductId"" = @CollectionProductId,
                        ""CollectionPrintifyProductId"" = @CollectionPrintifyProductId
                    WHERE ""Id"" = @Id";
                const string insertItem = @"
                    INSERT INTO public.""OrderItems"" (""Id"", ""OrderId"", ""ProductId"", ""Quantity"", ""VariantId"", ""PrintProviderId"", ""Cost"", ""ShippingCost"", ""Status"", ""Metadata"", ""DateSentToProduction"", ""DateFulfilled"", ""ProjectId"", ""CollectionId"", ""CollectionProductId"", ""CollectionPrintifyProductId"")
                    VALUES (@Id, @OrderId, @ProductId, @Quantity, @VariantId, @PrintProviderId, @Cost, @ShippingCost, @Status, @Metadata, @DateSentToProduction, @DateFulfilled, @ProjectId, @CollectionId, @CollectionProductId, @CollectionPrintifyProductId)";

                foreach (var item in items)
                {
                    var key = $"{item.ProductId}|{item.VariantId}";
                    if (existingItemMap.TryGetValue(key, out var existingItem))
                    {
                        item.Id = existingItem.Id;
                        item.OrderId = order.Id;
                        await _dbConnection.ExecuteAsync(updateItem, item);
                        existingItemMap.Remove(key);
                    }
                    else
                    {
                        item.Id = Guid.NewGuid();
                        item.OrderId = order.Id;
                        await _dbConnection.ExecuteAsync(insertItem, item);
                    }
                }

                const string deleteAnswers = @"DELETE FROM public.""OrderItemAnswers"" WHERE ""OrderItemId"" = @id";
                const string deleteArtworks = @"DELETE FROM public.""OrderItemArtworks"" WHERE ""OrderItemId"" = @id";
                const string deleteItem = @"DELETE FROM public.""OrderItems"" WHERE ""Id"" = @id";
                foreach (var leftover in existingItemMap.Values)
                {
                    await _dbConnection.ExecuteAsync(deleteAnswers, new { id = leftover.Id });
                    await _dbConnection.ExecuteAsync(deleteArtworks, new { id = leftover.Id });
                    await _dbConnection.ExecuteAsync(deleteItem, new { id = leftover.Id });
                }

                const string deleteShipments = @"DELETE FROM public.""OrderShipments"" WHERE ""OrderId"" = @id";
                const string insertShipment = @"
                    INSERT INTO public.""OrderShipments"" (""Id"", ""OrderId"", ""Carrier"", ""Number"", ""Url"", ""DeliveredAt"")
                    VALUES (@Id, @OrderId, @Carrier, @Number, @Url, @DeliveredAt)";
                await _dbConnection.ExecuteAsync(deleteShipments, new { id = order.Id });
                foreach (var shipment in shipments)
                {
                    shipment.Id = Guid.NewGuid();
                    shipment.OrderId = order.Id;
                    await _dbConnection.ExecuteAsync(insertShipment, shipment);
                }

                return new SyncResultItem { IsUpdated = true };
            }
        }

        async Task InsertItemsAndShipmentsAsync(Guid orderId, List<OrderItem> items, List<OrderShipment> shipments)
        {
            const string insertItem = @"
                INSERT INTO public.""OrderItems"" (""Id"", ""OrderId"", ""ProductId"", ""Quantity"", ""VariantId"", ""PrintProviderId"", ""Cost"", ""ShippingCost"", ""Status"", ""Metadata"", ""DateSentToProduction"", ""DateFulfilled"", ""ProjectId"", ""CollectionId"", ""CollectionProductId"", ""CollectionPrintifyProductId"")
                VALUES (@Id, @OrderId, @ProductId, @Quantity, @VariantId, @PrintProviderId, @Cost, @ShippingCost, @Status, @Metadata, @DateSentToProduction, @DateFulfilled, @ProjectId, @CollectionId, @CollectionProductId, @CollectionPrintifyProductId)";

            foreach (var item in items)
            {
                item.Id = Guid.NewGuid();
                item.OrderId = orderId;
                await _dbConnection.ExecuteAsync(insertItem, item);
            }

            const string insertShipment = @"
                INSERT INTO public.""OrderShipments"" (""Id"", ""OrderId"", ""Carrier"", ""Number"", ""Url"", ""DeliveredAt"")
                VALUES (@Id, @OrderId, @Carrier, @Number, @Url, @DeliveredAt)";

            foreach (var shipment in shipments)
            {
                shipment.Id = Guid.NewGuid();
                shipment.OrderId = orderId;
                await _dbConnection.ExecuteAsync(insertShipment, shipment);
            }
        }
    }
}
