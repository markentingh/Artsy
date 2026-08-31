using Microsoft.AspNetCore.Http;

namespace Artsy.API.Models.Collections
{
    public class ReplaceMockupImageRequest
    {
        public Guid ProjectId { get; set; }
        public Guid CollectionId { get; set; }
        public Guid MockupId { get; set; }
        public IFormFile File { get; set; } = null!;
    }
}
