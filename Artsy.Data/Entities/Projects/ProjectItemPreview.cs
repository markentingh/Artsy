namespace Artsy.Data.Entities.Projects
{
    public class ProjectItemPreview
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid ItemId { get; set; }
        public DateTime Created { get; set; }
        public string ImageModel { get; set; } = "";
        public string ImageModelJson { get; set; } = "";
    }
}
