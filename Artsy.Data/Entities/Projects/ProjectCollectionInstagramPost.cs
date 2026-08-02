namespace Artsy.Data.Entities.Projects
{
    public class ProjectCollectionInstagramPost
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid CollectionId { get; set; }
        public Guid InstagramAccountId { get; set; }
        public string Description { get; set; } = "";
        public string ContainerId { get; set; } = "";
        public string? Permalink { get; set; }
        public int Status { get; set; } = 1;
        public DateTime Created { get; set; }
    }
}
