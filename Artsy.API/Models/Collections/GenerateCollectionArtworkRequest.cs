using System.Text.Json.Serialization;
using Artsy.API.Models.Projects;

namespace Artsy.API.Models.Collections
{
    public class GenerateCollectionArtworkRequest
    {
        [JsonPropertyName("projectId")]
        public Guid ProjectId { get; set; }

        [JsonPropertyName("collectionId")]
        public Guid CollectionId { get; set; }

        [JsonPropertyName("itemId")]
        public Guid ItemId { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("answers")]
        public List<GenerateProjectItemPreviewAnswer> Answers { get; set; } = new();

        [JsonPropertyName("requestedChanges")]
        public string? RequestedChanges { get; set; }

        [JsonPropertyName("isFullSize")]
        public bool IsFullSize { get; set; }
    }
}
