using System.Text.Json.Serialization;

namespace Artsy.API.Models.Collections
{
    public class AddCollectionArtworkReferenceRequest
    {
        [JsonPropertyName("collectionId")]
        public Guid CollectionId { get; set; }

        [JsonPropertyName("itemId")]
        public Guid ItemId { get; set; }

        [JsonPropertyName("customImageId")]
        public Guid CustomImageId { get; set; }
    }

    public class DeleteCollectionArtworkReferenceRequest
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }
    }
}
