namespace Artsy.Data.Entities.Projects
{
    public class ProjectItemListDto
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public int Index { get; set; }
        public string? Title { get; set; }
        public bool SocialMedia { get; set; }
        public int ProductCount { get; set; }
        public int QuestionCount { get; set; }
    }

    public class ProjectItemThumbnailDto
    {
        public Guid Id { get; set; }
        public Guid ItemId { get; set; }
    }
}
