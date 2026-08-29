using System.Text.Json.Serialization;

namespace Artsy.API.Models.Collections
{
    public class UpdateCollectionArtworkOptionalPromptRequest
    {
        [JsonPropertyName("collectionId")]
        public Guid CollectionId { get; set; }

        [JsonPropertyName("itemId")]
        public Guid ItemId { get; set; }

        [JsonPropertyName("optionalPrompt")]
        public string? OptionalPrompt { get; set; }
    }
}
