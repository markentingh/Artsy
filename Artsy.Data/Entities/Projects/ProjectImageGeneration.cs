namespace Artsy.Data.Entities.Projects
{
    public class ProjectImageGeneration
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid? ItemId { get; set; }
        public Guid? CollectionId { get; set; }
        public Guid? BlueprintId { get; set; }
        public int InputTextTokens { get; set; }
        public int InputImageTokens { get; set; }
        public int OutputTokens { get; set; }
        public string ImageModel { get; set; } = "";
        public string Prompt { get; set; } = "";
        public string Filename { get; set; } = "";
        public bool HasThumbnail { get; set; }
        public bool IsFullSize { get; set; }
        public DateTime DateCreated { get; set; }
    }
}
