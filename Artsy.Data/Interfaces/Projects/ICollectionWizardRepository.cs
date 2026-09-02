using Artsy.Data.Entities.Projects;
using Artsy.Data.Entities;

namespace Artsy.Data.Interfaces.Projects
{
    public interface ICollectionWizardRepository
    {
        Task<CollectionWizardData> LoadAsync(Guid projectId, Guid? collectionId = null);
    }

    public class CollectionWizardData
    {
        public List<ProjectQuestion> Questions { get; set; } = new();
        public List<ProjectItemListDto> Items { get; set; } = new();
        public List<ProjectItemArtwork> ItemArtwork { get; set; } = new();
        public List<ProjectItemThumbnailDto> RefThumbnails { get; set; } = new();
        public List<ProjectItemThumbnailDto> PreviewThumbnails { get; set; } = new();
        public List<ProjectItemReference> ItemReferences { get; set; } = new();
        public List<ProjectBlueprintListDto> Blueprints { get; set; } = new();
        public List<ProjectBlueprintProductImage> BlueprintProductImages { get; set; } = new();
        public List<PrintifyBlueprintImage> PrintifyImages { get; set; } = new();
        public List<PrintifyBlueprintImageVariant> PrintifyImageVariants { get; set; } = new();
        public List<PrintifyBlueprintVariant> PrintifyVariants { get; set; } = new();

        // Collection-specific (null when no collectionId)
        public List<ProjectCollectionAnswer> Answers { get; set; }
        public List<ProjectCollectionArtwork> Artwork { get; set; }
        public List<ProjectCollectionArtworkPlacement> ArtworkPlacements { get; set; }
        public List<ProjectCollectionPrintifyProduct> PrintifyProducts { get; set; }
        public List<ProjectCollectionPrintifyProductMockup> Mockups { get; set; }
        public List<ProjectCollectionInstagramPost> InstagramPosts { get; set; }
        public List<ProjectCollectionProduct> CollectionProducts { get; set; }
        public List<ProjectCollectionProductImage> ProductImages { get; set; }
    }
}
