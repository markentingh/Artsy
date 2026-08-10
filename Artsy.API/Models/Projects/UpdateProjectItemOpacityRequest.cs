namespace Artsy.API.Models.Projects
{
    public class UpdateProjectItemOpacityRequest
    {
        public Guid ItemId { get; set; }
        public string? OpacityJson { get; set; }
    }
}
