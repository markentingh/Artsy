using System.Text.Json.Serialization;

namespace Artsy.API.Models.Projects
{
    public class CreateProductBlueprintImageRequest
    {
        [JsonPropertyName("projectId")]
        public Guid ProjectId { get; set; }

        [JsonPropertyName("projectBlueprintId")]
        public Guid ProjectBlueprintId { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("variantColor")]
        public string VariantColor { get; set; } = "";

        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = "";

        [JsonPropertyName("imageId")]
        public Guid? ImageId { get; set; }
    }

    public class UpdateProductBlueprintImageRequest
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("variantColor")]
        public string VariantColor { get; set; } = "";

        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = "";

        [JsonPropertyName("imageId")]
        public Guid? ImageId { get; set; }
    }

    public class DeleteProductBlueprintImageRequest
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }
    }
}
