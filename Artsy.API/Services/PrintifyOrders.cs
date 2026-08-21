using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Artsy.Data.Entities.Auth;
using Artsy.Data.Entities.Orders;
using Artsy.Data.Interfaces.Auth;
using Artsy.Data.Interfaces.Orders;
using Artsy.Data.Interfaces.Projects;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Models;

namespace Artsy.API.Services
{
    public interface IPrintifyOrders
    {
        Task<(int New, int Updated)> RefreshForUserAsync(Guid appUserId);
        Task<(int New, int Updated)> RefreshAllAsync();
        Task<(int New, int Updated)> CheckAndRunAllAsync();
    }

    public class PrintifyOrders : IPrintifyOrders
    {
        readonly IHttpClientFactory _httpClientFactory;
        readonly IAppUserRepository _userRepository;
        readonly IOrderRepository _orderRepository;
        readonly IHangfireOrderRepository _hangfireOrderRepository;
        readonly IProjectCollectionProductRepository _projectCollectionProductRepository;
        readonly IConfiguration _configuration;

        const string BaseUrl = "https://api.printify.com/v1";

        public PrintifyOrders(IHttpClientFactory httpClientFactory, IAppUserRepository userRepository, IOrderRepository orderRepository, IHangfireOrderRepository hangfireOrderRepository, IProjectCollectionProductRepository projectCollectionProductRepository, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _userRepository = userRepository;
            _orderRepository = orderRepository;
            _hangfireOrderRepository = hangfireOrderRepository;
            _projectCollectionProductRepository = projectCollectionProductRepository;
            _configuration = configuration;
        }

        public async Task<(int New, int Updated)> RefreshAllAsync()
        {
            var shops = await _orderRepository.GetDistinctActiveShopsAsync();
            return await RefreshShopsAsync(shops);
        }

        public async Task<(int New, int Updated)> RefreshForUserAsync(Guid appUserId)
        {
            var shops = (await _orderRepository.GetDistinctActiveShopsAsync())
                .Where(s => s.AppUserId == appUserId)
                .ToList();
            return await RefreshShopsAsync(shops);
        }

        public async Task<(int New, int Updated)> CheckAndRunAllAsync()
        {
            var interval = _configuration.GetValue<int?>("Hangfire:Orders:Intervals") ?? 1440;
            if (interval <= 0)
                return (0, 0);

            var latest = await _hangfireOrderRepository.GetLatestAsync();
            if (latest != null && latest.DateChecked > DateTime.UtcNow.AddMinutes(-interval))
                return (0, 0);

            var (n, u) = await RefreshAllAsync();
            await _hangfireOrderRepository.AddAsync(new HangfireOrder { NewOrders = n, UpdatedOrders = u, DateChecked = DateTime.UtcNow });
            return (n, u);
        }

        async Task<(int, int)> RefreshShopsAsync(IEnumerable<PrintifyShopWithUser> shops)
        {
            var totalNew = 0;
            var totalUpdated = 0;
            foreach (var shop in shops)
            {
                var (n, u) = await RefreshShopAsync(shop.AppUserId, shop.PrintifyShopId);
                totalNew += n;
                totalUpdated += u;
            }
            return (totalNew, totalUpdated);
        }

        async Task<(int, int)> RefreshShopAsync(Guid appUserId, int shopId)
        {
            var token = await GetAccessTokenAsync(appUserId);
            if (string.IsNullOrEmpty(token))
                return (0, 0);

            using var client = CreatePrintifyClient(token);
            var since = DateTime.UtcNow.AddDays(-14);
            var page = 1;
            var newCount = 0;
            var updatedCount = 0;

            while (page <= 100)
            {
                var response = await client.GetAsync($"{BaseUrl}/shops/{shopId}/orders.json?page={page}");
                if (!response.IsSuccessStatusCode)
                    break;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("data", out var data) || data.GetArrayLength() == 0)
                    break;

                foreach (var orderEl in data.EnumerateArray())
                {
                    var createdAt = ParseDateTime(orderEl, "created_at");
                    if (createdAt.HasValue && createdAt.Value < since)
                        continue;

                    var (order, items, shipments, hash) = await MapOrder(appUserId, shopId, orderEl);
                    var result = await _orderRepository.SyncOrderAsync(order, items, shipments, hash);
                    if (result.IsNew) newCount++;
                    else if (result.IsUpdated) updatedCount++;
                }

                if (data.GetArrayLength() > 0)
                {
                    var lastOrder = data[data.GetArrayLength() - 1];
                    var lastCreatedAt = ParseDateTime(lastOrder, "created_at");
                    if (lastCreatedAt.HasValue && lastCreatedAt.Value < since)
                        break;
                }

                page++;
            }

            return (newCount, updatedCount);
        }

        async Task<(Order, List<OrderItem>, List<OrderShipment>, string)> MapOrder(Guid appUserId, int shopId, JsonElement orderEl)
        {
            var order = new Order
            {
                AppUserId = appUserId,
                PrintifyShopId = shopId,
                OrderId = GetString(orderEl, "id"),
                AppOrderId = GetString(orderEl, "app_order_id"),
                AddressTo = GetJson(orderEl, "address_to"),
                Metadata = GetJson(orderEl, "metadata"),
                TotalPrice = GetInt(orderEl, "total_price"),
                TotalShipping = GetInt(orderEl, "total_shipping"),
                TotalTax = GetInt(orderEl, "total_tax"),
                Status = GetString(orderEl, "status"),
                ShippingMethod = GetInt(orderEl, "shipping_method"),
                IsExpress = GetBool(orderEl, "is_printify_express"),
                IsEconomyShipping = GetBool(orderEl, "is_economy_shipping"),
                DateCreated = ParseDateTime(orderEl, "created_at"),
                DateSentToProduction = ParseDateTime(orderEl, "sent_to_production_at"),
                DateFulfilled = ParseDateTime(orderEl, "fulfilled_at"),
                PrintifyConnect = GetJson(orderEl, "printify_connect"),
                ResponseJson = orderEl.GetRawText(),
            };

            var items = new List<OrderItem>();
            if (orderEl.TryGetProperty("line_items", out var lineItems))
            {
                foreach (var item in lineItems.EnumerateArray())
                {
                    var title = GetString(item, "title");
                    if (item.TryGetProperty("metadata", out var itemMetadata) && string.IsNullOrWhiteSpace(title))
                        title = GetString(itemMetadata, "title");

                    var blueprintId = GetInt(item, "blueprint_id");
                    var cp = await _projectCollectionProductRepository.GetByNameAndBlueprintIdAsync(title, blueprintId);

                    var printifyProductId = GetString(item, "product_id");

                    items.Add(new OrderItem
                    {
                        ProductId = printifyProductId,
                        Quantity = GetInt(item, "quantity"),
                        VariantId = GetInt(item, "variant_id"),
                        PrintProviderId = GetInt(item, "print_provider_id"),
                        Cost = GetInt(item, "cost"),
                        ShippingCost = GetInt(item, "shipping_cost"),
                        Status = GetString(item, "status"),
                        Metadata = GetJson(item, "metadata"),
                        DateSentToProduction = ParseDateTime(item, "sent_to_production_at"),
                        DateFulfilled = ParseDateTime(item, "fulfilled_at"),
                        ProjectId = cp?.ProjectId ?? Guid.Empty,
                        CollectionId = cp?.CollectionId ?? Guid.Empty,
                        CollectionProductId = cp?.Id ?? Guid.Empty,
                        CollectionPrintifyProductId = Guid.Empty,
                    });
                }
            }

            var shipments = new List<OrderShipment>();
            if (orderEl.TryGetProperty("shipments", out var shipItems))
            {
                foreach (var ship in shipItems.EnumerateArray())
                {
                    shipments.Add(new OrderShipment
                    {
                        Carrier = GetString(ship, "carrier"),
                        Number = GetString(ship, "number"),
                        Url = GetString(ship, "url"),
                        DeliveredAt = ParseDateTime(ship, "delivered_at"),
                    });
                }
            }

            var hash = ComputeHash(orderEl.GetRawText());
            return (order, items, shipments, hash);
        }

        async Task<string?> GetAccessTokenAsync(Guid userId)
        {
            var user = await _userRepository.FindByGuidAsync(userId);
            var token = user?.PrintifyAccessToken;
            if (string.IsNullOrEmpty(token))
                token = ConnectionSettings.PrintifyApiToken;
            return token;
        }

        HttpClient CreatePrintifyClient(string accessToken)
        {
            return IPv4HttpClientHelper.CreateHttpClient(_httpClientFactory, accessToken);
        }

        static string GetJson(JsonElement el, string name)
        {
            if (!el.TryGetProperty(name, out var prop) || prop.ValueKind == JsonValueKind.Null) return "";
            return prop.GetRawText();
        }

        static string GetString(JsonElement el, string name)
        {
            if (!el.TryGetProperty(name, out var prop) || prop.ValueKind == JsonValueKind.Null) return "";
            return prop.GetString() ?? "";
        }

        static int GetInt(JsonElement el, string name)
        {
            if (!el.TryGetProperty(name, out var prop) || prop.ValueKind == JsonValueKind.Null) return 0;
            return prop.ValueKind == JsonValueKind.Number ? prop.GetInt32() : 0;
        }

        static bool GetBool(JsonElement el, string name)
        {
            if (!el.TryGetProperty(name, out var prop) || prop.ValueKind == JsonValueKind.Null) return false;
            return prop.ValueKind == JsonValueKind.True;
        }

        static DateTime? ParseDateTime(JsonElement el, string name)
        {
            if (!el.TryGetProperty(name, out var prop) || prop.ValueKind == JsonValueKind.Null) return null;
            var s = prop.GetString();
            if (DateTimeOffset.TryParse(s, out var dto)) return dto.UtcDateTime;
            return null;
        }

        static string ComputeHash(string input)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes);
        }
    }
}
