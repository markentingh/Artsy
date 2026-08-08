using System.Text.Json.Serialization;

namespace Artsy.AI.Models
{
    public class ChatChoice
    {
        [JsonPropertyName("message")]
        public ChatMessage Message { get; set; } = new ChatMessage();
    }
}
