using System.Text.Json.Serialization;

namespace Artsy.API.Models.Collections
{
    public class EstimateCollectionTokensRequest
    {
        [JsonPropertyName("projectId")]
        public Guid ProjectId { get; set; }
    }
}
