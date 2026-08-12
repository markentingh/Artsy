namespace Artsy.PrintifyScraper.Models
{
    public class PrintProviderInfo
    {
        public int PrintProviderId { get; set; }
        public string Name { get; set; } = "";
        public List<ProviderColor> Colors { get; set; } = new List<ProviderColor>();
    }
}
