using System.Text.Json.Serialization;

namespace Artsy.API.Models.Projects
{
    public class UpdateSocialMediaConfigRequest
    {
        public Guid Id { get; set; }
        public string? SocialMediaPrompt { get; set; }
        public string? SocialMediaDescription { get; set; }
    }
}
