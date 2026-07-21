namespace Artsy.API.Models.Projects
{
    public class UpdateProjectItemArtworkTypeRequest
    {
        public Guid ItemId { get; set; }
        public string ArtworkType { get; set; } = "ai";
        public Guid? CustomImageId { get; set; }
    }
}
