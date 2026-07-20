namespace Artsy.Data.Entities.Projects
{
    public class ProjectItem
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public int Index { get; set; }
        public string? Title { get; set; }
        public bool SocialMedia { get; set; }
    }
}
