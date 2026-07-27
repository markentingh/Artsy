namespace Artsy.Data.Entities.Projects
{
    public class ProjectCollectionPrintifyProduct
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid CollectionId { get; set; }
        public Guid ProductId { get; set; }
        public string PrintifyProductId { get; set; } = "";
        public int PrintifyShopId { get; set; }
        public int PrintifyUserId { get; set; }
        public int ProviderId { get; set; }
        public bool Published { get; set; }
        public int Status { get; set; } = 1;
        public DateTime Created { get; set; }
    }
}
