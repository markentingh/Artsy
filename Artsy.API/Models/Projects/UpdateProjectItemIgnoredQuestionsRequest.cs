namespace Artsy.API.Models.Projects
{
    public class UpdateProjectItemIgnoredQuestionsRequest
    {
        public Guid ItemId { get; set; }
        public List<Guid> IgnoredQuestionIds { get; set; } = new();
    }
}
