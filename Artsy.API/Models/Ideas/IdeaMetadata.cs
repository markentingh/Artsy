namespace Artsy.API.Models.Ideas
{
    public class IdeaMetadata
    {
        public Dictionary<string, string> ArtworkDescriptions { get; set; } = new Dictionary<string, string>();
        public List<string> UsedTitles { get; set; } = new List<string>();
    }
}
