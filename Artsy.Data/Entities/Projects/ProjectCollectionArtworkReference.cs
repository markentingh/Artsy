namespace Artsy.Data.Entities.Projects
{
    public class ProjectCollectionArtworkReference
    {
        public Guid Id { get; set; }
        public Guid CollectionId { get; set; }
        public Guid ProjectId { get; set; }
        public Guid ItemId { get; set; }
        public Guid CustomImageId { get; set; }
        public DateTime Created { get; set; }
    }
}
