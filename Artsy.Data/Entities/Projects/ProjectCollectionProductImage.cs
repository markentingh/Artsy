namespace Artsy.Data.Entities.Projects
{
    public class ProjectCollectionProductImage
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid CollectionId { get; set; }
        public Guid ProjectBlueprintId { get; set; }
        public int Variant { get; set; }
        public int Placement { get; set; }
        public string ImageModel { get; set; } = "";
        public string Prompt { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
        public bool Accepted { get; set; }
        public string ResponseId { get; set; } = "";
    }
}
