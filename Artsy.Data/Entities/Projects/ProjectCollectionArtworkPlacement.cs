namespace Artsy.Data.Entities.Projects
{
    public class ProjectCollectionArtworkPlacement
    {
        public Guid Id { get; set; }
        public Guid CollectionArtworkId { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Index { get; set; }
        public bool FullSize { get; set; }
        public string PrintifyImageId { get; set; } = "";
        public string ResponseId { get; set; } = "";
        public Guid? GroupId { get; set; }
        public string Position { get; set; } = "";
        public string OptionalPrompt { get; set; } = "";
    }
}
