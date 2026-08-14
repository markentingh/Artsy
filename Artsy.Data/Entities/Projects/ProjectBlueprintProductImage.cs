namespace Artsy.Data.Entities.Projects
{
    public class ProjectBlueprintProductImage
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid ProjectBlueprintId { get; set; }
        public string Title { get; set; } = "";
        public string VariantColor { get; set; } = "";
        public int Status { get; set; } = 1;
        public string Prompt { get; set; } = "";
        public Guid? ImageId { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime DateUpdated { get; set; }
    }
}
