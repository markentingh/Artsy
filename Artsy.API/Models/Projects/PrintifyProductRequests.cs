namespace Artsy.API.Models.Projects
{
    public class CreatePrintifyProductRequest
    {
        public Guid CollectionId { get; set; }
        public Guid ProjectBlueprintId { get; set; }
    }

    public class UpdatePrintifyProductRequest
    {
        public Guid CollectionId { get; set; }
        public Guid ProductId { get; set; }
    }

    public class PublishPrintifyProductRequest
    {
        public Guid CollectionId { get; set; }
        public Guid ProductId { get; set; }
    }

    public class UnpublishPrintifyProductRequest
    {
        public Guid CollectionId { get; set; }
        public Guid ProductId { get; set; }
    }

    public class DeletePrintifyProductRequest
    {
        public Guid CollectionId { get; set; }
        public Guid ProductId { get; set; }
    }

    public class EnsureProductsRequest
    {
        public Guid CollectionId { get; set; }
    }

    public class UploadArtworkImageRequest
    {
        public Guid CollectionId { get; set; }
        public Guid ArtworkId { get; set; }
    }

    public class DownloadMockupsRequest
    {
        public Guid CollectionId { get; set; }
        public Guid ProjectBlueprintId { get; set; }
    }
}
