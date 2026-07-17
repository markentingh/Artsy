using System.Text.Json.Serialization;

namespace Artsy.API.Models.Projects
{
    public class OpenAIImageResponse
    {
        [JsonPropertyName("data")]
        public List<OpenAIImageData>? Data { get; set; }
    }

    public class OpenAIImageData
    {
        [JsonPropertyName("b64_json")]
        public string? B64Json { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }
}
