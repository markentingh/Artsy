using System.Text.Json.Serialization;

namespace Artsy.API.Models.Ideas
{
    public class MakeCollectionRequest
    {
        [JsonPropertyName("variationId")]
        public Guid VariationId { get; set; }
    }
}
