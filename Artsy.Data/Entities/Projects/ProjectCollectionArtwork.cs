namespace Artsy.Data.Entities.Projects
{
    public class ProjectCollectionArtwork
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid CollectionId { get; set; }
        public Guid ItemId { get; set; }
        public bool Active { get; set; }
        public int Images { get; set; }
        public string ImageModel { get; set; } = "";
        public string ImageModelJson { get; set; } = "";
        public string Prompt { get; set; } = "";
    }
}
