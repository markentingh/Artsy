namespace Artsy.API.Models.Projects
{
    public class UpdateProjectPostToInstagramRequest
    {
        public Guid Id { get; set; }
        public bool PostToInstagram { get; set; }
    }
}
