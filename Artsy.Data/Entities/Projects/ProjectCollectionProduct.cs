namespace Artsy.Data.Entities.Projects
{
    public class ProjectCollectionProduct
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid CollectionId { get; set; }
        public Guid ProjectBlueprintId { get; set; }
        public int BlueprintId { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string SafetyInfo { get; set; } = "";
        public string PricingJson { get; set; } = "[]";
        public bool Active { get; set; } = true;
    }
}
