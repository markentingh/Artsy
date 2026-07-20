namespace Artsy.API.Models.Projects
{
    public class CreateProjectBlueprintRequest
    {
        public Guid ProjectId { get; set; }
        public int BlueprintId { get; set; }
        public string Name { get; set; } = "";
        public string BlueprintJson { get; set; } = "";
        public string PlacementJson { get; set; } = "";
    }
}
