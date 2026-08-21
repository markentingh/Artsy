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
        /// <summary>
        /// When set, overrides the size calculated from Width/Height.
        /// Pass a custom size string like "1920x1072" for GPT image 2.0 custom sizes.
        /// </summary>
        public string? CustomSize { get; set; }
        public string Quality { get; set; } = "medium";
        public string? PreviousResponseId { get; set; }
        public bool UseResponsesApi { get; set; }
    }
}
