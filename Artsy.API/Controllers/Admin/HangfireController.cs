using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Artsy.Auth.Policies;
using Artsy.Data.Entities.Orders;
using Artsy.Data.Interfaces.Orders;

namespace Artsy.API.Controllers.Admin
{
    [Authorize(Policy = nameof(AuthConstants.Policy.ManageUsers))]
    [Route("/api/admin/hangfire")]
    public class HangfireController : ApiController
    {
        readonly IHangfireOrderRepository _hangfireOrderRepository;

        public HangfireController(IHangfireOrderRepository hangfireOrderRepository)
        {
            _hangfireOrderRepository = hangfireOrderRepository;
        }

        [HttpGet("orders-history")]
        public async Task<IActionResult> GetOrdersHistory(string range = "24h")
        {
            var now = DateTime.UtcNow;
            var since = range switch
            {
                "7d" => now.AddDays(-7),
                "30d" => now.AddDays(-30),
                "12m" => now.AddMonths(-12),
                "ytd" => new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                _ => now.AddHours(-24)
            };

            var records = (await _hangfireOrderRepository.GetByDateRangeAsync(since)).ToList();
            var bars = BuildBars(range, records);

            return Json(new { success = true, data = bars });
        }

        static List<Dictionary<string, object>> BuildBars(string range, List<HangfireOrder> records)
        {
            var now = DateTime.UtcNow;
            var bars = new List<Dictionary<string, object>>();

            if (range == "24h")
            {
                var latestHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);
                for (int i = 23; i >= 0; i--)
                {
                    var bucketStart = latestHour.AddHours(-i);
                    var bucketEnd = bucketStart.AddHours(1);
                    var newOrders = records.Where(r => r.DateChecked >= bucketStart && r.DateChecked < bucketEnd).Sum(r => r.NewOrders);
                    var updatedOrders = records.Where(r => r.DateChecked >= bucketStart && r.DateChecked < bucketEnd).Sum(r => r.UpdatedOrders);
                    bars.Add(BuildBar(bucketStart.ToString("h tt"), bucketStart.ToString("g"), newOrders, updatedOrders));
                }
            }
            else if (range == "7d" || range == "30d")
            {
                int days = range == "7d" ? 7 : 30;
                for (int i = days - 1; i >= 0; i--)
                {
                    var bucketStart = now.Date.AddDays(-i);
                    var bucketEnd = bucketStart.AddDays(1);
                    var newOrders = records.Where(r => r.DateChecked >= bucketStart && r.DateChecked < bucketEnd).Sum(r => r.NewOrders);
                    var updatedOrders = records.Where(r => r.DateChecked >= bucketStart && r.DateChecked < bucketEnd).Sum(r => r.UpdatedOrders);
                    bars.Add(BuildBar(bucketStart.ToString("M/d"), bucketStart.ToString("D"), newOrders, updatedOrders));
                }
            }
            else if (range == "12m")
            {
                var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                for (int i = 11; i >= 0; i--)
                {
                    var bucketStart = monthStart.AddMonths(-i);
                    var bucketEnd = bucketStart.AddMonths(1);
                    var newOrders = records.Where(r => r.DateChecked >= bucketStart && r.DateChecked < bucketEnd).Sum(r => r.NewOrders);
                    var updatedOrders = records.Where(r => r.DateChecked >= bucketStart && r.DateChecked < bucketEnd).Sum(r => r.UpdatedOrders);
                    bars.Add(BuildBar(bucketStart.ToString("MMM"), bucketStart.ToString("Y"), newOrders, updatedOrders));
                }
            }
            else if (range == "ytd")
            {
                var yearStart = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                for (int w = 0; w < 52; w++)
                {
                    var bucketStart = yearStart.AddDays(w * 7);
                    var bucketEnd = bucketStart.AddDays(7);
                    var newOrders = records.Where(r => r.DateChecked >= bucketStart && r.DateChecked < bucketEnd).Sum(r => r.NewOrders);
                    var updatedOrders = records.Where(r => r.DateChecked >= bucketStart && r.DateChecked < bucketEnd).Sum(r => r.UpdatedOrders);
                    var title = $"{bucketStart.ToString("M/d")} - {bucketEnd.AddDays(-1).ToString("M/d")}";
                    bars.Add(BuildBar($"W{w + 1}", title, newOrders, updatedOrders));
                }
            }

            return bars;
        }

        static Dictionary<string, object> BuildBar(string label, string title, int newOrders, int updatedOrders)
        {
            return new Dictionary<string, object>
            {
                ["label"] = label,
                ["title"] = title,
                ["newOrders"] = newOrders,
                ["updatedOrders"] = updatedOrders,
            };
        }
    }
}
