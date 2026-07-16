namespace Artsy.Data.Entities.Projects
{
    public class ProjectItem
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public int Index { get; set; }
    }
}
