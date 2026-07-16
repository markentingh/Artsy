namespace Artsy.Data.Entities.Projects
{
    public class ProjectCollection
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = "";
        public DateTime Created { get; set; }
    }
}
