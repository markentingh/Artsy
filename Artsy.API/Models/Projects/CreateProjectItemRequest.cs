namespace Artsy.API.Models.Projects
{
    public class CreateProjectItemRequest
    {
        public Guid ProjectId { get; set; }
        public string? Title { get; set; }
    }
}
