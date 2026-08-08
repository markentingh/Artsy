using System.Text.Json.Serialization;

namespace Artsy.API.Models.Ideas
{
    public class CreateIdeaRequest
    {
        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = "";
    }
}
