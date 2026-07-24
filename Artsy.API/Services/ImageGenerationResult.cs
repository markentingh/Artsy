namespace Artsy.API.Services
{
    public class ImageGenerationResult
    {
        public byte[] ImageBytes { get; set; } = Array.Empty<byte>();
        public string? ResponseId { get; set; }
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
    }
}
