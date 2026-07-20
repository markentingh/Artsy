namespace Artsy.Data.Entities
{
    public class PrintifyBlueprintPrintProvider
    {
        public int BlueprintId { get; set; }
        public int PrintProviderId { get; set; }
        public string Title { get; set; } = "";
        public string DecorationMethods { get; set; } = "[]";
        public DateTime DateUpdated { get; set; }
    }
}
