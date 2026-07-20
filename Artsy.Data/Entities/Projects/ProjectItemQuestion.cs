namespace Artsy.Data.Entities.Projects
{
    public class ProjectItemQuestion
    {
        public Guid Id { get; set; }
        public Guid ItemId { get; set; }
        public Guid ProjectId { get; set; }
        public int Index { get; set; }
        public string Question { get; set; } = "";
    }
}
