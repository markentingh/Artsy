using System.Text.Json;
using Artsy.API.Models.Trends;

namespace Artsy.API.Services
{
    public interface ITrendResearchService
    {
        string[] GetSectors();
        Task<List<RisingKeyword>> GetRisingKeywordsAsync(string sector);
        Task<List<int>> GetInterestOverTimeAsync(string keyword);
        Task<int> GetEtsyListingCountAsync(string keyword);
    }

    public class TrendResearchService : ITrendResearchService
    {
        readonly IHttpClientFactory _httpClientFactory;

        static readonly string[] Sectors = new[]
        {
            "Home & Garden",
            "Apparel",
            "Arts & Entertainment",
            "Toys & Hobbies",
            "Craft Supplies",
            "Fashion"
        };

        public TrendResearchService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public string[] GetSectors() => Sectors;

        public async Task<List<RisingKeyword>> GetRisingKeywordsAsync(string seedKeyword)
        {
            var apiKey = ConnectionSettings.SerpApiKey;
            if (string.IsNullOrEmpty(apiKey))
                throw new InvalidOperationException("SerpApi key is not configured.");

            var client = _httpClientFactory.CreateClient();
            var url = $"https://serpapi.com/search.json?engine=google_trends&q={Uri.EscapeDataString(seedKeyword)}&data_type=RELATED_QUERIES&date=now+1-m&api_key={apiKey}";

            var response = await client.GetAsync(url);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"SerpApi error: {response.StatusCode} - {responseContent}");

            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var serpResponse = JsonSerializer.Deserialize<SerpApiRelatedQueriesResponse>(responseContent, jsonOptions);

            if (serpResponse?.RelatedQueries?.Rising == null)
                return new List<RisingKeyword>();

            var rising = serpResponse.RelatedQueries.Rising;
            var result = new List<RisingKeyword>();

            foreach (var item in rising)
            {
                var isBreakout = string.IsNullOrEmpty(item.Value) || item.Value.Equals("Breakout", StringComparison.OrdinalIgnoreCase);
                var isHighGrowth = item.ExtractedValue > 500;

                if (isBreakout || isHighGrowth)
                {
                    result.Add(new RisingKeyword
                    {
                        Keyword = item.Query,
                        TrendType = isBreakout ? "Breakout" : $"+{item.ExtractedValue}%"
                    });
                }
            }

            return result;
        }

        public async Task<List<int>> GetInterestOverTimeAsync(string keyword)
        {
            var apiKey = ConnectionSettings.SerpApiKey;
            if (string.IsNullOrEmpty(apiKey))
                return new List<int>();

            var client = _httpClientFactory.CreateClient();
            var url = $"https://serpapi.com/search.json?engine=google_trends&q={Uri.EscapeDataString(keyword)}&data_type=TIMESERIES&date=now+1-m&api_key={apiKey}";

            var response = await client.GetAsync(url);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return new List<int>();

            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var serpResponse = JsonSerializer.Deserialize<SerpApiTimeseriesResponse>(responseContent, jsonOptions);

            if (serpResponse?.InterestOverTime?.TimelineData == null)
                return new List<int>();

            return serpResponse.InterestOverTime.TimelineData
                .Select(entry => entry.Values?.FirstOrDefault()?.ExtractedValue ?? 0)
                .ToList();
        }

        public async Task<int> GetEtsyListingCountAsync(string keyword)
        {
            var apiKey = ConnectionSettings.EtsyKeystring;
            if (string.IsNullOrEmpty(apiKey))
                throw new InvalidOperationException("Etsy keystring is not configured.");

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("x-api-key", apiKey);

            var encodedKeyword = Uri.EscapeDataString(keyword);
            var url = $"https://openapi.etsy.com/v3/application/listings/active?keywords={encodedKeyword}&limit=1&offset=0";

            var response = await client.GetAsync(url);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Etsy API error: {response.StatusCode} - {responseContent}");

            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var etsyResponse = JsonSerializer.Deserialize<EtsySearchResponse>(responseContent, jsonOptions);

            return etsyResponse?.Count ?? 0;
        }
    }
}
