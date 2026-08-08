using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Artsy.AI.Models
{
    public class ChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<ChatChoice> Choices { get; set; } = new List<ChatChoice>();
    }
}
