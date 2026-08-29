using System.Text.Json.Serialization;

namespace Artsy.API.Models.Collections
{
    public class PatternSettingsDto
    {
        [JsonPropertyName("spacingX")]
        public double SpacingX { get; set; } = 1;

        [JsonPropertyName("spacingY")]
        public double SpacingY { get; set; } = 1;

        [JsonPropertyName("angle")]
        public double Angle { get; set; }

        [JsonPropertyName("offset")]
        public double Offset { get; set; }

        [JsonPropertyName("scale")]
        public double Scale { get; set; } = 0.5;
    }
}
