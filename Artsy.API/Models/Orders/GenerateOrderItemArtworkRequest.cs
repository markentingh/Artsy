using System.Text.Json.Serialization;

namespace Artsy.API.Models.Orders
{
    public class GenerateOrderItemArtworkRequest
    {
        [JsonPropertyName("orderId")]
        public Guid OrderId { get; set; }

        [JsonPropertyName("orderItemId")]
        public Guid OrderItemId { get; set; }

        [JsonPropertyName("artworkItemId")]
        public Guid ArtworkItemId { get; set; }

        [JsonPropertyName("modelId")]
        public int ModelId { get; set; }

        [JsonPropertyName("requestText")]
        public string RequestText { get; set; } = "";
    }
}
