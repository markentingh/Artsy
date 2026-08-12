using System.Text.Json.Serialization;

namespace Artsy.PrintifyScraper.Models
{
    public class ProviderColor
    {
        public string Name { get; set; } = "";
        public int R { get; set; } = -1;
        public int G { get; set; } = -1;
        public int B { get; set; } = -1;

        [JsonIgnore]
        public string Hex => R >= 0 && G >= 0 && B >= 0 ? $"#{R:X2}{G:X2}{B:X2}" : "";
    }
}
