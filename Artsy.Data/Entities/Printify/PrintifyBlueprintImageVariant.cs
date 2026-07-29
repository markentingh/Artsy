namespace Artsy.Data.Entities
{
    public class PrintifyBlueprintImageVariant
    {
        public Guid Id { get; set; }
        public Guid BlueprintImageId { get; set; }
        public string VariantColor { get; set; } = "";
        public DateTime DateCreated { get; set; }
        public DateTime DateUpdated { get; set; }
    }
}
