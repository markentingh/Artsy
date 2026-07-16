namespace Artsy.API.Models.Projects
{
    public class CreateProjectQuestionRequest
    {
        public Guid ProjectId { get; set; }
        public string Question { get; set; } = "";
        public int Index { get; set; }
    }
}
