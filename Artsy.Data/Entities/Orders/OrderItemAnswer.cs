namespace Artsy.Data.Entities.Orders
{
    public class OrderItemAnswer
    {
        public Guid Id { get; set; }
        public Guid OrderItemId { get; set; }
        public Guid ProjectId { get; set; }
        public Guid QuestionId { get; set; }
        public Guid? ItemId { get; set; }
        public string Answer { get; set; } = "";
    }
}
