namespace Artsy.API.Models.Ideas
{
    public class IdeaResult
    {
        public string Title { get; set; } = "";
        public List<IdeaVariationResult> Variations { get; set; } = new List<IdeaVariationResult>();
    }

    public class IdeaVariationResult
    {
        public string Title { get; set; } = "";
        public IdeaAnswersResult? Project { get; set; }
        public IdeaAnswersResult? Artworks { get; set; }
    }

    public class IdeaAnswersResult
    {
        public List<IdeaAnswerResult> Answers { get; set; } = new List<IdeaAnswerResult>();
    }

    public class IdeaAnswerResult
    {
        public string Id { get; set; } = "";
        public string Answer { get; set; } = "";
    }
}
