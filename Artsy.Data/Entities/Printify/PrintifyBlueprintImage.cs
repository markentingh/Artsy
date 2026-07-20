namespace Artsy.Data.Entities
{
    public class PrintifyBlueprintImage
    {
        public Guid Id { get; set; }
        public int BlueprintId { get; set; }
        public int ImageIndex { get; set; }
        public string Variants { get; set; } = "[]";
        public int Type { get; set; }
        public int Position { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime DateUpdated { get; set; }
    }
}
