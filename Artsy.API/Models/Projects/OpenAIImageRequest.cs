using System.Text.Json.Serialization;

namespace Artsy.API.Models.Projects
{
    public class OpenAIImageRequest
    {
        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("prompt")]
        public string? Prompt { get; set; }

        [JsonPropertyName("n")]
        public int? N { get; set; }

        [JsonPropertyName("size")]
        public string? Size { get; set; }

        [JsonPropertyName("quality")]
        public string? Quality { get; set; }
    }
}
