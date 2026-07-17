namespace Artsy.API.Models.Projects
{
    public class UpdateProjectItemTitleRequest
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
    }
}
