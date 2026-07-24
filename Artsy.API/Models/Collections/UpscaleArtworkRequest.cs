using System.Text.Json.Serialization;

namespace Artsy.API.Models.Collections
{
    public class UpscaleArtworkRequest
    {
        [JsonPropertyName("projectId")]
        public Guid ProjectId { get; set; }

        [JsonPropertyName("collectionId")]
        public Guid CollectionId { get; set; }

        [JsonPropertyName("itemId")]
        public Guid ItemId { get; set; }
    }
}
