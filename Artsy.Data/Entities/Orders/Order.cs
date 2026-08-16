namespace Artsy.Data.Entities.Orders
{
    public class Order
    {
        public Guid Id { get; set; }
        public Guid AppUserId { get; set; }
        public int PrintifyShopId { get; set; }
        public string OrderId { get; set; } = "";
        public string AppOrderId { get; set; } = "";
        public string AddressTo { get; set; } = "";
        public string Metadata { get; set; } = "";
        public int TotalPrice { get; set; }
        public int TotalShipping { get; set; }
        public int TotalTax { get; set; }
        public string Status { get; set; } = "";
        public int ShippingMethod { get; set; }
        public bool IsExpress { get; set; }
        public bool IsEconomyShipping { get; set; }
        public DateTime? DateCreated { get; set; }
        public DateTime? DateSentToProduction { get; set; }
        public DateTime? DateFulfilled { get; set; }
        public string PrintifyConnect { get; set; } = "";
        public string DataHash { get; set; } = "";
        public string ResponseJson { get; set; } = "";
        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }
    }
}
