using System.Text.Json.Serialization;

namespace Artsy.API.Models.Collections
{
    public class EditArtworkRequest
    {
        [JsonPropertyName("projectId")]
        public Guid ProjectId { get; set; }

        [JsonPropertyName("collectionId")]
        public Guid CollectionId { get; set; }

        [JsonPropertyName("itemId")]
        public Guid ItemId { get; set; }

        [JsonPropertyName("placementIndex")]
        public int? PlacementIndex { get; set; }

        [JsonPropertyName("groupId")]
        public Guid? GroupId { get; set; }

        [JsonPropertyName("rotate180")]
        public bool Rotate180 { get; set; }

        [JsonPropertyName("flipHorizontal")]
        public bool FlipHorizontal { get; set; }

        [JsonPropertyName("flipVertical")]
        public bool FlipVertical { get; set; }
    }
}
