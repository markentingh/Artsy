namespace Artsy.API.Models.Projects
{
    public class ProjectItemListItem
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public int Index { get; set; }
        public List<string> BlueprintNames { get; set; } = new();
    }
}
