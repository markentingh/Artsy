namespace Artsy.API.Models.Projects
{
    public class UpdateCollectionProductsActiveRequest
    {
        public Guid CollectionId { get; set; }
        public List<CollectionProductActive> Products { get; set; } = new();
    }

    public class CollectionProductActive
    {
        public Guid ProjectBlueprintId { get; set; }
        public bool Active { get; set; }
    }
}