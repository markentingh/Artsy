namespace Artsy.API.Models.Collections
{
    public class FixSeamlessPlacementsRequest
    {
        public Guid ProjectId { get; set; }
        public Guid CollectionId { get; set; }
        public Guid ItemId { get; set; }
    }
}
