namespace Artsy.API.Models.Projects
{
    public class UpdateItemBlueprintPlacementRequest
    {
        public Guid Id { get; set; }
        public string PlacementJson { get; set; } = "";
    }
}
