using System.Text.Json.Serialization;

namespace Artsy.API.Models.Collections
{
    public class PlacementDto
    {
        [JsonPropertyName("source")]
        public string Source { get; set; } = "";
        [JsonPropertyName("itemId")]
        public Guid? ItemId { get; set; }
        [JsonPropertyName("decorationMethod")]
        public string DecorationMethod { get; set; } = "";
        [JsonPropertyName("dimensions")]
        public string Dimensions { get; set; } = "";
        [JsonPropertyName("customImageId")]
        public Guid? CustomImageId { get; set; }
        [JsonPropertyName("customItemId")]
        public Guid? CustomItemId { get; set; }

        public Guid GetItemId()
        {
            if (Source == "item" && ItemId.HasValue) return ItemId.Value;
            if (Source == "custom" && CustomItemId.HasValue) return CustomItemId.Value;
            return Guid.Empty;
        }

        public (int Width, int Height) GetDimensions()
        {
            if (string.IsNullOrWhiteSpace(Dimensions)) return (0, 0);
            var parts = Dimensions.Split('x');
            if (parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h))
                return (w, h);
            return (0, 0);
        }
    }
}
