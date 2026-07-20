namespace Artsy.API.Models.Projects
{
    public class ReorderProjectItemsRequest
    {
        public Guid ProjectId { get; set; }
        public List<Guid> ItemIds { get; set; } = new();
    }
}
