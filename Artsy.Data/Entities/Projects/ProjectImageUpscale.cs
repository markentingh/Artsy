namespace Artsy.Data.Entities.Projects
{
    public class ProjectImageUpscale
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid CollectionId { get; set; }
        public Guid ItemId { get; set; }
        public Guid ArtworkId { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Scale { get; set; }
        public DateTime Created { get; set; }
    }
}
