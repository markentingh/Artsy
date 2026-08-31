using System.Text.Json.Serialization;

namespace Artsy.API.Models.Collections
{
    public class GenerateProductImageRequest
    {
        [JsonPropertyName("projectId")]
        public Guid ProjectId { get; set; }

        [JsonPropertyName("collectionId")]
        public Guid CollectionId { get; set; }

        [JsonPropertyName("projectBlueprintId")]
        public Guid? ProjectBlueprintId { get; set; }

        [JsonPropertyName("productImageId")]
        public Guid ProductImageId { get; set; }

        [JsonPropertyName("id")]
        public Guid? Id { get; set; }

        [JsonPropertyName("modelId")]
        public int? ModelId { get; set; }

        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = "";

        [JsonPropertyName("variantColor")]
        public string VariantColor { get; set; } = "";

        [JsonPropertyName("requestedChanges")]
        public string? RequestedChanges { get; set; }

        [JsonPropertyName("productName")]
        public string? ProductName { get; set; }

        [JsonPropertyName("mockupImageIds")]
        public List<Guid> MockupImageIds { get; set; } = new List<Guid>();

        [JsonPropertyName("includeArtworkRef")]
        public bool? IncludeArtworkRef { get; set; }
    }

    public class AcceptProductImageRequest
    {
        [JsonPropertyName("collectionId")]
        public Guid CollectionId { get; set; }

        [JsonPropertyName("productImageId")]
        public Guid ProductImageId { get; set; }
    }

    public class GetProductImageVariantsRequest
    {
        [JsonPropertyName("projectId")]
        public Guid ProjectId { get; set; }

        [JsonPropertyName("collectionId")]
        public Guid CollectionId { get; set; }
    }

    public class AutoAcceptCustomArtworkRequest
    {
        [JsonPropertyName("projectId")]
        public Guid ProjectId { get; set; }

        [JsonPropertyName("collectionId")]
        public Guid CollectionId { get; set; }

        [JsonPropertyName("itemId")]
        public Guid ItemId { get; set; }
    }

    public class DeleteCollectionArtworkRequest
    {
        [JsonPropertyName("collectionId")]
        public Guid CollectionId { get; set; }

        [JsonPropertyName("itemId")]
        public Guid ItemId { get; set; }
    }

    public class DeleteProductImageRequest
    {
        [JsonPropertyName("collectionId")]
        public Guid CollectionId { get; set; }

        [JsonPropertyName("projectBlueprintId")]
        public Guid? ProjectBlueprintId { get; set; }

        [JsonPropertyName("productImageId")]
        public Guid ProductImageId { get; set; }
    }

    public class GenerateArtworkThumbnailRequest
    {
        [JsonPropertyName("collectionId")]
        public Guid CollectionId { get; set; }

        [JsonPropertyName("itemId")]
        public Guid ItemId { get; set; }
    }

    public class DeactivateProductImagesRequest
    {
        [JsonPropertyName("collectionId")]
        public Guid CollectionId { get; set; }

        [JsonPropertyName("combos")]
        public List<DeleteProductImageRequest> Combos { get; set; } = new();
    }

    public class SyncProductImageSelectionsRequest
    {
        [JsonPropertyName("collectionId")]
        public Guid CollectionId { get; set; }

        [JsonPropertyName("projectId")]
        public Guid ProjectId { get; set; }

        [JsonPropertyName("selectedCombos")]
        public List<SyncComboDto> SelectedCombos { get; set; } = new();
    }

    public class SyncComboDto
    {
        [JsonPropertyName("projectBlueprintId")]
        public Guid ProjectBlueprintId { get; set; }

        [JsonPropertyName("productImageId")]
        public Guid ProductImageId { get; set; }
    }

    public class UpdateCollectionProductNameRequest
    {
        [JsonPropertyName("collectionId")]
        public Guid CollectionId { get; set; }

        [JsonPropertyName("projectBlueprintId")]
        public Guid ProjectBlueprintId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    public class AddCollectionProductImageRequest
    {
        [JsonPropertyName("projectId")]
        public Guid ProjectId { get; set; }

        [JsonPropertyName("collectionId")]
        public Guid CollectionId { get; set; }

        [JsonPropertyName("projectBlueprintId")]
        public Guid? ProjectBlueprintId { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = "";
    }

    public class UpdateCollectionProductImageConfigRequest
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("collectionId")]
        public Guid CollectionId { get; set; }

        [JsonPropertyName("variantColor")]
        public string VariantColor { get; set; } = "";

        [JsonPropertyName("imageModel")]
        public string ImageModel { get; set; } = "";

        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = "";

        [JsonPropertyName("selectedMockups")]
        public string SelectedMockups { get; set; } = "";

        [JsonPropertyName("includeArtworkRef")]
        public bool IncludeArtworkRef { get; set; } = true;
    }

    public class DeleteCollectionProductImageByIdRequest
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }
    }
}
