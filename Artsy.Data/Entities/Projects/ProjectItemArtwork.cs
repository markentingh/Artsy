namespace Artsy.Data.Entities.Projects
{
    public class ProjectItemArtwork
    {
        public Guid Id { get; set; }
        public Guid ItemId { get; set; }
        public Guid ProjectId { get; set; }
        public string ImageModel { get; set; } = "";
        public string Prompt { get; set; } = "";
        public string ArtworkType { get; set; } = "ai";
        public Guid? CustomImageId { get; set; }
        public string? IgnoredQuestions { get; set; }
        public string? OpacityJson { get; set; }
        public string AspectRatio { get; set; } = "1:1";
    }
}
