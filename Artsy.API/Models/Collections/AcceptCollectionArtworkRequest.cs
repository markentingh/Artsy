namespace Artsy.API.Models.Collections
{
    public class AcceptCollectionArtworkRequest
    {
        public Guid CollectionId { get; set; }
        public Guid ItemId { get; set; }
    }
}
