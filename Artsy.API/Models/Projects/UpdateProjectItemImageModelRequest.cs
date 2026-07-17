namespace Artsy.API.Models.Projects
{
    public class UpdateProjectItemImageModelRequest
    {
        public Guid ItemId { get; set; }
        public string ImageModel { get; set; } = "";
        public string ImageModelJson { get; set; } = "";
    }
}
