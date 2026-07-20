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

        [JsonPropertyName("images")]
        public List<OpenAIImageReference>? Images { get; set; }
    }

    public class OpenAIImageReference
    {
        [JsonPropertyName("image")]
        public string? Image { get; set; }

        [JsonPropertyName("detail")]
        public string? Detail { get; set; }
    }
}
