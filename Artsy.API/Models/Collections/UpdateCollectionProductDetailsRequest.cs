namespace Artsy.API.Models.Collections
{
    public class UpdateCollectionProductDetailsRequest
    {
        public Guid CollectionId { get; set; }
        public Guid ProjectBlueprintId { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string SafetyInfo { get; set; } = "";
        public string PricingJson { get; set; } = "[]";
        /// <summary>If true and a Printify product exists, update it via PUT API</summary>
        public bool UpdatePrintify { get; set; } = false;
        /// <summary>Which fields changed (name, description, safetyInfo, pricing) — used for partial Printify update</summary>
        public List<string> ChangedFields { get; set; } = new();
    }

    public class GenerateCollectionProductInfoRequest
    {
        public Guid CollectionId { get; set; }
        public Guid ProjectBlueprintId { get; set; }
    }
}
