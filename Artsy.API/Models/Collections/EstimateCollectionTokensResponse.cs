using System.Text.Json.Serialization;

namespace Artsy.API.Models.Collections
{
    public class EstimatePlacementDto
    {
        [JsonPropertyName("blueprintId")]
        public int BlueprintId { get; set; }

        [JsonPropertyName("blueprintName")]
        public string BlueprintName { get; set; } = "";

        [JsonPropertyName("position")]
        public string Position { get; set; } = "";

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }
    }

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

        [JsonPropertyName("needsRegeneration")]
        public bool NeedsRegeneration { get; set; }

        [JsonPropertyName("tokens")]
        public int Tokens { get; set; }

        [JsonPropertyName("placements")]
        public List<EstimatePlacementDto> Placements { get; set; } = new();
    }

    public class EstimateCollectionTokensResponse
    {
        [JsonPropertyName("generations")]
        public List<CollectionArtworkGenerationDto> Generations { get; set; } = new();

        [JsonPropertyName("totalTokens")]
        public int TotalTokens { get; set; }

        [JsonPropertyName("artworkCount")]
        public int ArtworkCount { get; set; }

        [JsonPropertyName("needsRegeneration")]
        public bool NeedsRegeneration { get; set; }
    }
}
