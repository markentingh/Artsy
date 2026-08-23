namespace Artsy.Data.Entities.Projects
{
    public class ProjectBlueprintPlacementGroupImage
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public int BlueprintId { get; set; }
        public Guid GroupId { get; set; }
        public int Index { get; set; }
        public Guid? ArtworkId { get; set; }
        public Guid? CustomId { get; set; }
        public string Position { get; set; }
        public bool FlipX { get; set; }
        public bool FlipY { get; set; }
    }
}
