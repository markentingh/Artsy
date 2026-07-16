namespace Artsy.API.Models.Projects
{
    public class UpdateProjectQuestionRequest
    {
        public Guid Id { get; set; }
        public string Question { get; set; } = "";
    }
}
