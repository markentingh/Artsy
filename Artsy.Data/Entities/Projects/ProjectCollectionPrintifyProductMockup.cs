namespace Artsy.Data.Entities.Projects
{
    public class ProjectCollectionPrintifyProductMockup
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid CollectionId { get; set; }
        public Guid PrintifyProductId { get; set; }
        public string VariantIds { get; set; } = "";
        public string Position { get; set; } = "";
        public string ImageUrl { get; set; } = "";
        public bool IsDefault { get; set; }
        public int Status { get; set; } = 1;
        public DateTime Created { get; set; }
    }
}
