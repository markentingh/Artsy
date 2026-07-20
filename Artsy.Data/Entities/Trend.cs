using System;

namespace Artsy.Data.Entities
{
    public class Trend
    {
        public Guid Id { get; set; }
        public string Keyword { get; set; } = string.Empty;
        public string Sector { get; set; } = string.Empty;
        public int EtsyListingCount { get; set; }
        public string? Data { get; set; }
        public DateTime DateCreated { get; set; }
    }
}
