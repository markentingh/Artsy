namespace Artsy.Data.Entities.Projects
{
    public class ProjectBlueprints
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public int BlueprintId { get; set; }
        public string Name { get; set; } = "";
        public string BlueprintJson { get; set; } = "";
        public string PlacementJson { get; set; } = "";
    }
}
