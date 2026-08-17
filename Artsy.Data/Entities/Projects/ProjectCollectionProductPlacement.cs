namespace Artsy.Data.Entities.Projects
{
    public class ProjectCollectionProductPlacement
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public Guid ArtworkId { get; set; }
        public string Position { get; set; } = "";
        public string VariantIds { get; set; } = "[]";
    }
}
