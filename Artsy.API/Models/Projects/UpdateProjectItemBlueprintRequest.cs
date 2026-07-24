namespace Artsy.API.Models.Projects
{
    public class UpdateProjectBlueprintRequest
    {
        public Guid Id { get; set; }
        public int BlueprintId { get; set; }
        public string Name { get; set; } = "";
        public string BlueprintJson { get; set; } = "";
        public string PlacementJson { get; set; } = "";
        public string Prompt { get; set; } = "";
    }
}
