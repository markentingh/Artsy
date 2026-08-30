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
        /// <summary>
        /// When set, uploads the specific placement variant instead of the base artwork.
        /// The PrintifyImageId is stored on the ProjectCollectionArtworkPlacement record.
        /// </summary>
        public int? PlacementIndex { get; set; }
        /// <summary>
        /// When set along with Position, used to find the exact placement for a seamless group.
        /// This avoids index collisions when multiple groups exist for the same artwork.
        /// </summary>
        public Guid? GroupId { get; set; }
        public string? Position { get; set; }
    }

    public class DownloadMockupsRequest
    {
        public Guid CollectionId { get; set; }
        public Guid ProjectBlueprintId { get; set; }
    }

    public class ArchiveUploadRequest
    {
        public Guid CollectionId { get; set; }
        public Guid ArtworkId { get; set; }
        public int? PlacementIndex { get; set; }
        public Guid? GroupId { get; set; }
        public string? GroupPosition { get; set; }
    }
}
