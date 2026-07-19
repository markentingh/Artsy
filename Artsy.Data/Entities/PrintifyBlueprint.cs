namespace Artsy.Data.Entities
{
    public class PrintifyBlueprint
    {
        public int BlueprintId { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Brand { get; set; } = "";
        public string Model { get; set; } = "";
        public int ImageCount { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime DateUpdated { get; set; }
    }
}
