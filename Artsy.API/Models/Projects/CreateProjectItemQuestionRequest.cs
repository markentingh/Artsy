namespace Artsy.API.Models.Projects
{
    public class CreateProjectItemQuestionRequest
    {
        public Guid ItemId { get; set; }
        public Guid ProjectId { get; set; }
        public string Question { get; set; } = "";
        public int Index { get; set; }
    }
}
