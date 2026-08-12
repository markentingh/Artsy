namespace Artsy.Data.Entities
{
    public class PrintifyBlueprintVariant
    {
        public int VariantId { get; set; }
        public int BlueprintId { get; set; }
        public int PrintProviderId { get; set; }
        public string Color { get; set; } = "";
        public string HexColor { get; set; } = "";
        public string Options { get; set; } = "{}";
        public string Size { get; set; } = "";
        public string? Depth { get; set; }
        public string? Design { get; set; }
        public string? Finish { get; set; }
        public string? Flavor { get; set; }
        public string? Hands { get; set; }
        public string? Length { get; set; }
        public string? Material { get; set; }
        public string? Paper { get; set; }
        public string? Quantity { get; set; }
        public string? Scent { get; set; }
        public string? Shape { get; set; }
        public string? Surface { get; set; }
        public string? Type { get; set; }
        public string? Voltage { get; set; }
        public string? Weight { get; set; }
        public string DecorationMethods { get; set; } = "[]";
        public DateTime DateUpdated { get; set; }
    }
}
