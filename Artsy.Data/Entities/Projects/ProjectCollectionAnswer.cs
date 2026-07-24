namespace Artsy.Data.Entities.Projects
{
    public class ProjectCollectionAnswer
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid CollectionId { get; set; }
        public Guid? QuestionId { get; set; }
        public Guid? ItemId { get; set; }
        public string Answer { get; set; } = "";
    }
}
