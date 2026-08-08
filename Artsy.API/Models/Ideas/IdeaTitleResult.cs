namespace Artsy.API.Models.Ideas
{
    public class IdeaTitleResult
    {
        public string Title { get; set; } = "";
        public Dictionary<string, string> ArtworkDescriptions { get; set; } = new Dictionary<string, string>();
    }
}
