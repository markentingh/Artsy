namespace Artsy.API.Models.Trends
{
    public class TrendGapResult
    {
        public string Keyword { get; set; } = string.Empty;
        public string Sector { get; set; } = string.Empty;
        public int EtsyListingCount { get; set; }
        public List<int> InterestDataPoints { get; set; } = new();
        public string TrendType { get; set; } = string.Empty;
    }

    public class RisingKeyword
    {
        public string Keyword { get; set; } = string.Empty;
        public string TrendType { get; set; } = string.Empty;
    }

    public class SerpApiRelatedQueriesResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("related_queries")]
        public SerpApiRelatedQueries? RelatedQueries { get; set; }
    }

    public class SerpApiRelatedQueries
    {
        [System.Text.Json.Serialization.JsonPropertyName("rising")]
        public List<SerpApiRisingQuery>? Rising { get; set; }
    }

    public class SerpApiRisingQuery
    {
        [System.Text.Json.Serialization.JsonPropertyName("query")]
        public string Query { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("value")]
        public string? Value { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("extracted_value")]
        public double ExtractedValue { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("link")]
        public string? Link { get; set; }
    }

    public class SerpApiTimeseriesResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("interest_over_time")]
        public SerpApiInterestOverTime? InterestOverTime { get; set; }
    }

    public class SerpApiInterestOverTime
    {
        [System.Text.Json.Serialization.JsonPropertyName("timeline_data")]
        public List<SerpApiTimelineEntry>? TimelineData { get; set; }
    }

    public class SerpApiTimelineEntry
    {
        [System.Text.Json.Serialization.JsonPropertyName("date")]
        public string? Date { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("timestamp")]
        public string? Timestamp { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("values")]
        public List<SerpApiTimelineValue>? Values { get; set; }
    }

    public class SerpApiTimelineValue
    {
        [System.Text.Json.Serialization.JsonPropertyName("query")]
        public string? Query { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("extracted_value")]
        public int ExtractedValue { get; set; }
    }

    public class EtsySearchResponse
    {
        public int Count { get; set; }
    }

    public class TrendProgressEvent
    {
        public string Stage { get; set; } = string.Empty;
        public object Data { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }
}
