using System.Text.Json.Serialization;

namespace Artsy.API.Models.Collections
{
    public class GenerateSocialMediaDescriptionRequest
    {
        [JsonPropertyName("collectionId")]
        public Guid CollectionId { get; set; }
    }
}
