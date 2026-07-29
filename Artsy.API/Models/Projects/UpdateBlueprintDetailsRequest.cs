namespace Artsy.API.Models.Projects
{
    public class UpdateBlueprintDetailsRequest
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Prompt { get; set; } = "";
        public string SafetyInfo { get; set; } = "";
    }
}
