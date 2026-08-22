namespace Artsy.Data.Entities.Orders
{
    public class OrderItemArtworkPlacement
    {
        public Guid Id { get; set; }
        public Guid OrderItemArtworkId { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Index { get; set; }
        public string ResponseId { get; set; } = "";
        public Guid? GroupId { get; set; }
        public string Position { get; set; } = "";
    }
}
