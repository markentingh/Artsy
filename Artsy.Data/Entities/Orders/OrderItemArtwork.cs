namespace Artsy.Data.Entities.Orders
{
    public class OrderItemArtwork
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public Guid OrderItemId { get; set; }
        public Guid ProjectId { get; set; }
        public Guid CollectionId { get; set; }
        public Guid ItemId { get; set; }
        public bool Active { get; set; } = true;
        public int Width { get; set; }
        public int Height { get; set; }
        public string ImageModel { get; set; } = "";
        public string Prompt { get; set; } = "";
        public bool Accepted { get; set; }
        public string ResponseId { get; set; } = "";
        public bool FullSize { get; set; }
        public int Index { get; set; }
        public string PrintifyImageId { get; set; } = "";
        public bool Opacity { get; set; }
        public string RequestText { get; set; } = "";
        public int PlacementIndex { get; set; }
        public int TotalPlacements { get; set; } = 1;
        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }
    }
}
