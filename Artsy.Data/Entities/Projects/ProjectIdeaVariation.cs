namespace Artsy.Data.Entities.Projects
{
    public class ProjectIdeaVariation
    {
        public Guid Id { get; set; }
        public Guid ProjectIdeaId { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string IdeaJson { get; set; } = "";
    }
}
