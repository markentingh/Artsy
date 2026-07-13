using System.Text.Json.Serialization;

namespace Artsy.API.Models.Printify
{
    public class PrintifyShop
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("sales_channel")]
        public string SalesChannel { get; set; } = "";
    }
}
