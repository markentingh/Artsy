namespace Artsy.API.Models.Projects
{
    public class ProjectBlueprintListResponse
    {
        public Guid Id { get; set; }
        public int BlueprintId { get; set; }
        public string Name { get; set; } = "";
        public string BlueprintJson { get; set; } = "";
        public bool Configured { get; set; }
        public int ImageCount { get; set; }
    }
}
