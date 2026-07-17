using System.Text.Json.Serialization;

namespace Artsy.API.Models.Projects
{
    public class GenerateProjectItemPreviewRequest
    {
        [JsonPropertyName("projectId")]
        public Guid ProjectId { get; set; }

        [JsonPropertyName("itemId")]
        public Guid ItemId { get; set; }

        [JsonPropertyName("imageModel")]
        public string ImageModel { get; set; } = "";

        [JsonPropertyName("imageModelJson")]
        public string ImageModelJson { get; set; } = "";

        [JsonPropertyName("answers")]
        public List<GenerateProjectItemPreviewAnswer> Answers { get; set; } = new();
    }
}
