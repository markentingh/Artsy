namespace Artsy.API.Models.Projects
{
    public class UpdateProjectItemQuestionRequest
    {
        public Guid Id { get; set; }
        public string Question { get; set; } = "";
    }
}
