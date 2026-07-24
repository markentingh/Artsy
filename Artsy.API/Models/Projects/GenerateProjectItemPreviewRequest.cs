using System.Text.Json.Serialization;

namespace Artsy.API.Models.Projects
{
    public class GenerateProjectItemPreviewRequest
    {
        [JsonPropertyName("itemId")]
        public Guid ItemId { get; set; }

        [JsonPropertyName("answers")]
        public List<GenerateProjectItemPreviewAnswer> Answers { get; set; } = new();
    }
}
