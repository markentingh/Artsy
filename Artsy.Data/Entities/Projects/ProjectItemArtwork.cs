namespace Artsy.Data.Entities.Projects
{
    public class ProjectItemArtwork
    {
        public Guid Id { get; set; }
        public Guid ItemId { get; set; }
        public Guid ProjectId { get; set; }
        public string ImageModel { get; set; } = "";
        public string ImageModelJson { get; set; } = "";
        public string Prompt { get; set; } = "";
    }
}
