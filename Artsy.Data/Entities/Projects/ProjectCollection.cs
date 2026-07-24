namespace Artsy.Data.Entities.Projects
{
    public class ProjectCollection
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string Title { get; set; } = "";
        public DateTime Created { get; set; }
        public int Status { get; set; } = 1;
    }
}
