namespace Artsy.Data.Entities.Projects
{
    public class ProjectBlueprintPlacementGroup
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public int BlueprintId { get; set; }
    }
}
