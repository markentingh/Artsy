using System.Text.Json.Serialization;

namespace Artsy.API.Models.Collections
{
    public class PatternSettingsDto
    {
        [JsonPropertyName("spacingX")]
        public double SpacingX { get; set; }

        [JsonPropertyName("spacingY")]
        public double SpacingY { get; set; }

        [JsonPropertyName("angle")]
        public double Angle { get; set; }

        [JsonPropertyName("offset")]
        public double Offset { get; set; }

        [JsonPropertyName("scale")]
        public double Scale { get; set; } = 0.5;
    }
}
