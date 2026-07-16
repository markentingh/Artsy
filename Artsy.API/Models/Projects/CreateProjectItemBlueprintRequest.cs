namespace Artsy.API.Models.Projects
{
    public class CreateProjectItemBlueprintRequest
    {
        public Guid ItemId { get; set; }
        public int BlueprintId { get; set; }
        public string Name { get; set; } = "";
        public string BlueprintJson { get; set; } = "";
    }
}
