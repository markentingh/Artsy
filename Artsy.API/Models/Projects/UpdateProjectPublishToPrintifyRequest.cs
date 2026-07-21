namespace Artsy.API.Models.Projects
{
    public class UpdateProjectPublishToPrintifyRequest
    {
        public Guid Id { get; set; }
        public bool PublishToPrintify { get; set; }
    }
}
