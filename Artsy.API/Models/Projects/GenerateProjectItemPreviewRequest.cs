using System.Text.Json.Serialization;

namespace Artsy.API.Models.Projects
{
    public class GenerateProjectItemPreviewRequest
    {
        [JsonPropertyName("itemId")]
        public Guid ItemId { get; set; }

        [JsonPropertyName("modelId")]
        public int ModelId { get; set; }

        [JsonPropertyName("answers")]
        public List<GenerateProjectItemPreviewAnswer> Answers { get; set; } = new();

        [JsonPropertyName("design")]
        public string? Design { get; set; }

        [JsonPropertyName("collectionId")]
        public Guid? CollectionId { get; set; }
    }
}
