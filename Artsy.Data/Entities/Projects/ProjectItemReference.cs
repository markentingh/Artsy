namespace Artsy.Data.Entities.Projects
{
    public class ProjectItemReference
    {
        public Guid Id { get; set; }
        public Guid ItemId { get; set; }
        public Guid ProjectId { get; set; }
        public string FileName { get; set; } = "";
        public string Extension { get; set; } = ".jpg";
        public Guid? ArtworkId { get; set; }
        public DateTime Created { get; set; }
    }
}
