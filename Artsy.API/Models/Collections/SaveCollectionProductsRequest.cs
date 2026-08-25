namespace Artsy.API.Models.Collections
{
    public class SaveCollectionProductsRequest
    {
        public Guid CollectionId { get; set; }
        public List<CollectionProductSelection> Products { get; set; } = new();
    }

    public class CollectionProductSelection
    {
        public Guid ProjectBlueprintId { get; set; }
        public bool Active { get; set; }
    }
}
