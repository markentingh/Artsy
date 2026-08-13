using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Artsy.API.Services
{
    public class OrdersBackgroundService : BackgroundService
    {
        readonly IServiceProvider _serviceProvider;
        readonly ILogger<OrdersBackgroundService> _logger;

        public OrdersBackgroundService(IServiceProvider serviceProvider, ILogger<OrdersBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                if (stoppingToken.IsCancellationRequested) break;

                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var orders = scope.ServiceProvider.GetRequiredService<IPrintifyOrders>();
                    var (n, u) = await orders.RefreshAllAsync();
                    _logger.LogInformation("Orders refresh complete. New: {New}, Updated: {Updated}", n, u);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Orders refresh failed.");
                }
            }
        }
    }
}
