namespace Artsy.API.Services
{
    public class ImageGenerationRequest
    {
        public string Model { get; set; } = "";
        public string Prompt { get; set; } = "";
        public List<byte[]> InputImages { get; set; } = new();
        public byte[]? InputMask { get; set; }
        public int Width { get; set; } = 1024;
        public int Height { get; set; } = 1024;
        public string Quality { get; set; } = "medium";
        public string? PreviousResponseId { get; set; }
        public bool UseResponsesApi { get; set; }
    }
}
