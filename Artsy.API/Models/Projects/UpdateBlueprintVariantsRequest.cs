namespace Artsy.API.Models.Projects
{
    public class UpdateBlueprintVariantsRequest
    {
        public Guid Id { get; set; }
        public string BlueprintJson { get; set; } = "";
        public int PrintProviderId { get; set; }
    }
}
