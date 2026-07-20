namespace Artsy.API.Models.Projects
{
    public class UpdateProjectItemSocialMediaRequest
    {
        public Guid Id { get; set; }
        public bool SocialMedia { get; set; }
    }
}
