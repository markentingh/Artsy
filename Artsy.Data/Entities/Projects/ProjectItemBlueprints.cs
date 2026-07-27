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
        public string Prompt { get; set; } = "";
        public int Status { get; set; } = 1;
        public string Description { get; set; } = "";
        public string SafetyInfo { get; set; } = "";
        public string PricingJson { get; set; } = "[]";
        public int PrintProviderId { get; set; }
    }
}
