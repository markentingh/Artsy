namespace Artsy.Data.Entities.Projects
{
    public class ProjectQuestion
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public int Index { get; set; }
        public string Question { get; set; } = "";
    }
}
