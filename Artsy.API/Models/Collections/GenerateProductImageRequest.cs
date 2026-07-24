using System.Text.Json.Serialization;

namespace Artsy.API.Models.Collections
{
    public class GenerateProductImageRequest
    {
        [JsonPropertyName("projectId")]
        public Guid ProjectId { get; set; }

        [JsonPropertyName("collectionId")]
        public Guid CollectionId { get; set; }

        [JsonPropertyName("projectBlueprintId")]
        public Guid ProjectBlueprintId { get; set; }

        [JsonPropertyName("variant")]
        public int Variant { get; set; }

        [JsonPropertyName("placement")]
        public int Placement { get; set; }

        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = "";

        [JsonPropertyName("requestedChanges")]
        public string? RequestedChanges { get; set; }
    }

    public class AcceptProductImageRequest
    {
        [JsonPropertyName("collectionId")]
        public Guid CollectionId { get; set; }

        [JsonPropertyName("productImageId")]
        public Guid ProductImageId { get; set; }
    }

    public class GetProductImageVariantsRequest
    {
        [JsonPropertyName("projectId")]
        public Guid ProjectId { get; set; }

        [JsonPropertyName("collectionId")]
        public Guid CollectionId { get; set; }
    }
}
