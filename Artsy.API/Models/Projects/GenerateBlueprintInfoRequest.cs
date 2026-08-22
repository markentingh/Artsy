namespace Artsy.API.Models.Projects
{
    public class GenerateBlueprintInfoRequest
    {
        public Guid Id { get; set; }
    }

    public class GenerateBlueprintInfoResponse
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
    }
}
