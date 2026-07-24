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
