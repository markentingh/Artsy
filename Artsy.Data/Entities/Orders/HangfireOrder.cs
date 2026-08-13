namespace Artsy.Data.Entities.Orders
{
    public class HangfireOrder
    {
        public Guid Id { get; set; }
        public DateTime DateChecked { get; set; }
        public int NewOrders { get; set; }
        public int UpdatedOrders { get; set; }
    }
}
