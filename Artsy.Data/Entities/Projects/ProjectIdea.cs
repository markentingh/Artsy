namespace Artsy.Data.Entities.Projects
{
    public class ProjectIdea
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string Title { get; set; } = "";
        public string Prompt { get; set; } = "";
        public string MetadataJson { get; set; } = "";
        public DateTime Created { get; set; }
    }
}
