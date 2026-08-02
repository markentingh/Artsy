namespace Artsy.Data.Entities.Projects
{
    public class Project
    {
        public Guid Id { get; set; }
        public Guid AppUserId { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string Key { get; set; } = "";
        public string Color { get; set; } = "";
        public int Status { get; set; }
        public bool PublishToPrintify { get; set; } = true;
        public bool PostToInstagram { get; set; } = true;
        public int? PrintifyStoreId { get; set; }
        public Guid? InstagramId { get; set; }
        public string? SocialMediaPrompt { get; set; }
        public string? SocialMediaDescription { get; set; }
        public DateTime Created { get; set; }
    }
}
