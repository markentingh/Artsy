using Artsy.Data.Interfaces;
using Artsy.PrintifyScraper.Models;
using Artsy.PrintifyScraper.Services;
using Microsoft.AspNetCore.SignalR;

namespace Artsy.API.Hubs
{
    public class PrintifyScraperHub : Hub
    {
        readonly IPrintifyScraperService _scraperService;
        readonly IPrintifyBlueprintRepository _blueprintRepo;

        public PrintifyScraperHub(
            IPrintifyScraperService scraperService,
            IPrintifyBlueprintRepository blueprintRepo)
        {
            _scraperService = scraperService;
            _blueprintRepo = blueprintRepo;
        }

        public async Task<object> GetProviderColors(int blueprintId)
        {
            try
            {
                var bp = await _blueprintRepo.GetByBlueprintIdAsync(blueprintId);
                if (bp == null)
                    return new { success = false, message = "Blueprint not found" };

                var connectionId = Context.ConnectionId;
                var progress = new Progress<string>(msg =>
                {
                    _ = Clients.Client(connectionId).SendAsync("PrintifyScraperProgress", new
                    {
                        stage = "scraper-message",
                        data = new { message = msg },
                        timestamp = DateTime.UtcNow
                    });
                });

                var providerInfos = await _scraperService.ScrapeProviderColorsAsync(bp.BlueprintId, bp.Brand, bp.Title, progress);

                if (providerInfos.Count == 0)
                    return new { success = false, message = "No provider colors found." };

                return new
                {
                    success = true,
                    data = new
                    {
                        blueprintId = bp.BlueprintId,
                        providers = providerInfos.Select(p => new
                        {
                            printProviderId = p.PrintProviderId,
                            name = p.Name,
                            colors = p.Colors.Select(c => new { name = c.Name, r = c.R, g = c.G, b = c.B, hex = c.Hex })
                        })
                    }
                };
            }
            catch (Exception ex)
            {
                return new { success = false, message = ex.Message };
            }
        }
    }
}
