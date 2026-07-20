namespace Artsy.Data.Entities
{
    public class PrintifyBlueprintVariant
    {
        public int VariantId { get; set; }
        public int BlueprintId { get; set; }
        public int PrintProviderId { get; set; }
        public string Title { get; set; } = "";
        public string Options { get; set; } = "{}";
        public string DecorationMethods { get; set; } = "[]";
        public DateTime DateUpdated { get; set; }
    }
}
