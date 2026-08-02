namespace Artsy.Data.Entities.Projects
{
    public class ProjectCollectionInstagramPostImage
    {
        public Guid Id { get; set; }
        public Guid InstagramPostId { get; set; }
        public Guid? ProductImageId { get; set; }
        public Guid? ArtworkId { get; set; }
        public int SortOrder { get; set; }
        public DateTime Created { get; set; }
    }
}
