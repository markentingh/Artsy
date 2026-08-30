using System.Text.Json.Serialization;

namespace Artsy.API.Models.Collections
{
    public class UpdatePlacementOptionalPromptRequest
    {
        [JsonPropertyName("collectionId")]
        public Guid CollectionId { get; set; }

        [JsonPropertyName("itemId")]
        public Guid ItemId { get; set; }

        [JsonPropertyName("placementIndex")]
        public int PlacementIndex { get; set; }

        [JsonPropertyName("groupId")]
        public Guid? GroupId { get; set; }

        [JsonPropertyName("optionalPrompt")]
        public string? OptionalPrompt { get; set; }
    }
}
