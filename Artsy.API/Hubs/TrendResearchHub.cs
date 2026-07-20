using System.Text.Json;
using Artsy.API.Models.Trends;
using Artsy.API.Services;
using Artsy.Data.Entities;
using Artsy.Data.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Artsy.API.Hubs
{
    public class TrendResearchHub : Hub
    {
        readonly ITrendResearchService _trendResearchService;
        readonly ITrendRepository _trendRepository;

        public TrendResearchHub(ITrendResearchService trendResearchService, ITrendRepository trendRepository)
        {
            _trendResearchService = trendResearchService;
            _trendRepository = trendRepository;
        }

        public async Task StartResearch(string seedKeyword)
        {
            try
            {
                await SendProgressAsync("fetching-trends", $"Fetching rising keywords for \"{seedKeyword}\"...");

                var risingKeywords = await _trendResearchService.GetRisingKeywordsAsync(seedKeyword);
                if (risingKeywords.Count == 0)
                {
                    await SendProgressAsync("complete", "No rising keywords found.");
                    await Clients.Caller.SendAsync("TrendResearchComplete", new { success = true, data = new List<TrendGapResult>() });
                    return;
                }

                await SendProgressAsync("fetching-trends", $"Found {risingKeywords.Count} rising keywords. Analyzing Etsy supply...");

                var results = new List<TrendGapResult>();
                var total = risingKeywords.Count;
                var processed = 0;
                var gapsFound = 0;

                foreach (var kw in risingKeywords)
                {
                    processed++;
                    await SendProgressAsync("analyzing", new
                    {
                        keyword = kw.Keyword,
                        processed,
                        total,
                        gapsFound,
                        message = $"Checking Etsy supply for \"{kw.Keyword}\" ({processed}/{total})..."
                    });

                    var etsyCount = await _trendResearchService.GetEtsyListingCountAsync(kw.Keyword);
                    if (etsyCount > 50)
                        continue;

                    await SendProgressAsync("gap-found", new
                    {
                        keyword = kw.Keyword,
                        etsyCount,
                        processed,
                        total,
                        gapsFound = gapsFound + 1,
                        message = $"Market gap found: \"{kw.Keyword}\" — {etsyCount} Etsy listings"
                    });

                    var interestData = await _trendResearchService.GetInterestOverTimeAsync(kw.Keyword);

                    var result = new TrendGapResult
                    {
                        Keyword = kw.Keyword,
                        Sector = seedKeyword,
                        EtsyListingCount = etsyCount,
                        InterestDataPoints = interestData,
                        TrendType = kw.TrendType
                    };

                    var dataJson = JsonSerializer.Serialize(new
                    {
                        interestDataPoints = interestData,
                        etsyListingCount = etsyCount,
                        trendType = kw.TrendType,
                        seedKeyword
                    });

                    await _trendRepository.CreateAsync(new Trend
                    {
                        Keyword = kw.Keyword,
                        Sector = seedKeyword,
                        EtsyListingCount = etsyCount,
                        Data = dataJson
                    });

                    gapsFound++;
                    results.Add(result);
                }

                await SendProgressAsync("complete", new
                {
                    totalAnalyzed = total,
                    gapsFound,
                    message = $"Done! Found {gapsFound} market gaps from {total} keywords."
                });

                var sorted = results
                    .OrderByDescending(r => r.EtsyListingCount == 0 ? 0 : 1)
                    .ThenBy(r => r.EtsyListingCount)
                    .Take(20)
                    .ToList();

                await Clients.Caller.SendAsync("TrendResearchComplete", new { success = true, data = sorted });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("TrendResearchComplete", new { success = false, message = ex.Message });
            }
        }

        async Task SendProgressAsync(string stage, object data)
        {
            await Clients.Caller.SendAsync("TrendResearchProgress", new TrendProgressEvent
            {
                Stage = stage,
                Data = data,
                Timestamp = DateTime.UtcNow
            });
        }
    }
}
