namespace Artsy.Data.Entities.Projects
{
    public class CustomImage
    {
        public Guid Id { get; set; }
        public Guid AppUserId { get; set; }
        public string FileName { get; set; } = "";
        public string Extension { get; set; } = ".jpg";
        public DateTime Created { get; set; }
    }
}
