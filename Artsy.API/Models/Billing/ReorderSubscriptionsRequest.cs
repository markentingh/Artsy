namespace Artsy.API.Models.Billing
{
    public class ReorderSubscriptionsRequest
    {
        public List<int> Ids { get; set; } = new();
    }
}
