using System.Text.Json.Serialization;

namespace Artsy.API.Models.Collections
{
    public class CollectionArtworkGenerationDto
    {
        [JsonPropertyName("itemId")]
        public Guid ItemId { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("needsUpscale")]
        public bool NeedsUpscale { get; set; } = true;
    }

    public class EstimateCollectionTokensResponse
    {
        [JsonPropertyName("generations")]
        public List<CollectionArtworkGenerationDto> Generations { get; set; } = new();

        [JsonPropertyName("totalTokens")]
        public int TotalTokens { get; set; }

        [JsonPropertyName("artworkCount")]
        public int ArtworkCount { get; set; }
    }
}
