namespace Artsy.Data.Entities.Projects
{
    public class ProjectCollectionArtwork
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid CollectionId { get; set; }
        public Guid ItemId { get; set; }
        public bool Active { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string ImageModel { get; set; } = "";
        public string Prompt { get; set; } = "";
        public bool Accepted { get; set; }
        public string ResponseId { get; set; } = "";
        public bool FullSize { get; set; }
    }
}
