namespace Artsy.Data.Entities.Projects
{
    public class ProjectItemReference
    {
        public Guid Id { get; set; }
        public Guid ItemId { get; set; }
        public Guid ProjectId { get; set; }
        public Guid? CustomImageId { get; set; }
        public Guid? ArtworkId { get; set; }
        public DateTime Created { get; set; }
    }
}
