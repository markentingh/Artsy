namespace Artsy.API.Models.Projects
{
    public class UpdateProjectTitleRequest
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = "";
    }
}
