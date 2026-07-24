using System.Text.Json.Serialization;

namespace Artsy.API.Models.Collections
{
    public class CreateCollectionRequest
    {
        [JsonPropertyName("projectId")]
        public Guid ProjectId { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = "";
    }
}
