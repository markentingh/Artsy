namespace Artsy.API.Models.Projects
{
    public class PostToSocialMediaRequest
    {
        public Guid ProjectId { get; set; }
        public Guid CollectionId { get; set; }
        public string Description { get; set; } = "";
        public List<SocialMediaImageItem> Images { get; set; } = new();
    }

    public class SocialMediaImageItem
    {
        public Guid? ProductImageId { get; set; }
        public Guid? ArtworkId { get; set; }
        public Guid? ItemId { get; set; }
        public int SortOrder { get; set; }
    }
}
