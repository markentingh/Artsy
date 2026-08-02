using System.Text.Json.Serialization;

namespace Artsy.API.Models.Collections
{
    public class UpdateCollectionTitleRequest
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = "";
    }
}
