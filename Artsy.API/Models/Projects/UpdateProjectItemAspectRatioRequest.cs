using System.Text.Json.Serialization;

namespace Artsy.API.Models.Projects
{
    public class UpdateProjectItemAspectRatioRequest
    {
        [JsonPropertyName("itemId")]
        public Guid ItemId { get; set; }

        [JsonPropertyName("aspectRatio")]
        public string AspectRatio { get; set; } = "1:1";
    }
}
