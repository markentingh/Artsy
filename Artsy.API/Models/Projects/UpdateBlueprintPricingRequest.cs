namespace Artsy.API.Models.Projects
{
    public class UpdateBlueprintPricingRequest
    {
        public Guid Id { get; set; }
        public string PricingJson { get; set; } = "[]";
    }
}
