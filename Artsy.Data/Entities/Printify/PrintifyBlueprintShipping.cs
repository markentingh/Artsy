namespace Artsy.Data.Entities
{
    public class PrintifyBlueprintShipping
    {
        public int BlueprintId { get; set; }
        public int PrintProviderId { get; set; }
        public int HandlingTimeValue { get; set; }
        public string HandlingTimeUnit { get; set; } = "day";
        public string Profiles { get; set; } = "[]";
        public DateTime DateUpdated { get; set; }
    }
}
