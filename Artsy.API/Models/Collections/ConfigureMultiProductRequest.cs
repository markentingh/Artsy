using System.Text.Json.Serialization;

namespace Artsy.API.Models.Collections
{
    public class SaveMultiProductJsonRequest
    {
        [JsonPropertyName("collectionId")]
        public Guid CollectionId { get; set; }

        [JsonPropertyName("multiProductJson")]
        public string MultiProductJson { get; set; } = "";
    }

    public class GetMultiProductJsonRequest
    {
        [JsonPropertyName("collectionId")]
        public Guid CollectionId { get; set; }
    }

    public class GenerateMultiProductInfoRequest
    {
        [JsonPropertyName("collectionId")]
        public Guid CollectionId { get; set; }
    }
}
