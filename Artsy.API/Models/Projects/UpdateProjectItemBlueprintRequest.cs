namespace Artsy.API.Models.Projects
{
    public class UpdateProjectItemBlueprintRequest
    {
        public Guid Id { get; set; }
        public int BlueprintId { get; set; }
        public string Name { get; set; } = "";
        public string BlueprintJson { get; set; } = "";
    }
}
