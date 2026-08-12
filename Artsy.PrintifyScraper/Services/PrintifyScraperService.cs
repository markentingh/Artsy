using Microsoft.Playwright;
using Artsy.PrintifyScraper.Models;

namespace Artsy.PrintifyScraper.Services
{
    public interface IPrintifyScraperService
    {
        Task<List<PrintProviderInfo>> ScrapeProviderColorsAsync(int blueprintId, string brand, string title, IProgress<string>? progress = null, CancellationToken cancellationToken = default);
        Task InitializeAsync();
    }

    public class PrintifyScraperService : IPrintifyScraperService
    {
        private IPlaywright? _playwright;
        private IBrowser? _browser;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private bool _browsersInstalled = false;

        public async Task InitializeAsync()
        {
            if (_playwright != null) return;
            _playwright = await Playwright.CreateAsync();
        }

        async Task EnsureBrowsersInstalledAsync(IProgress<string>? progress)
        {
            if (_browsersInstalled && _browser != null) return;
            try
            {
                _browser = await _playwright!.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
                _browsersInstalled = true;
            }
            catch (PlaywrightException)
            {
                // Browsers not installed — run the install command
                progress?.Report("Downloading new browsers for Playwright...");
                await InstallBrowsersAsync();
                _browser = await _playwright!.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
                _browsersInstalled = true;
            }
        }

        async Task InstallBrowsersAsync()
        {
            var exitCode = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
            if (exitCode != 0)
                throw new InvalidOperationException($"Failed to install Playwright browsers (exit code {exitCode}).");
        }

        string BuildPrintifyUrl(int blueprintId, string brand, string title)
        {
            var brandSlug = Slugify(brand);
            var titleSlug = Slugify(title);
            return $"https://printify.com/app/products/{blueprintId}/{brandSlug}/{titleSlug}";
        }

        string Slugify(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            return text.ToLowerInvariant()
                .Replace("&", "and")
                .Replace("+", "plus");
            // Note: further regex sanitization done below
        }

        string FullSlug(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            var slug = text.ToLowerInvariant();
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"&", "and");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"\+", "plus");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9]+", "-");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"^-+|-+$", "");
            return slug;
        }

        public async Task<List<PrintProviderInfo>> ScrapeProviderColorsAsync(int blueprintId, string brand, string title, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                await InitializeAsync();
                await EnsureBrowsersInstalledAsync(progress);
                if (_browser == null) throw new InvalidOperationException("Browser not initialized");

                var url = $"https://printify.com/app/products/{blueprintId}/{FullSlug(brand)}/{FullSlug(title)}";
                progress?.Report($"Navigating to {url}...");

                var page = await _browser.NewPageAsync();
                try
                {
                    await page.GotoAsync(url, new PageGotoOptions { Timeout = 30000, WaitUntil = WaitUntilState.NetworkIdle });
                    progress?.Report("Page loaded, looking for print providers...");

                    // Wait for print provider containers
                    var providerContainers = page.Locator("pfa-blueprint-print-provider");
                    try
                    {
                        await providerContainers.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });
                    }
                    catch
                    {
                        throw new Exception("No print provider sections found on the page.");
                    }

                    var allProviderContainers = await providerContainers.AllAsync();
                    var providerInfos = new List<PrintProviderInfo>();
                    var processedProviderIds = new HashSet<int>();

                    foreach (var providerContainer in allProviderContainers)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var pplink = providerContainer.Locator("a[data-testid='pplink']").First;
                        if (await pplink.CountAsync() == 0)
                        {
                            progress?.Report("Warning: Provider container without a pplink, skipping.");
                            continue;
                        }

                        var pplinkHref = await pplink.GetAttributeAsync("href") ?? "";
                        var pplinkMatch = System.Text.RegularExpressions.Regex.Match(pplinkHref, @"/print-provider/(\d+)");
                        if (!pplinkMatch.Success)
                        {
                            progress?.Report("Warning: Could not extract print provider ID from pplink.");
                            continue;
                        }

                        var printProviderId = int.Parse(pplinkMatch.Groups[1].Value);
                        if (processedProviderIds.Contains(printProviderId))
                            continue;
                        processedProviderIds.Add(printProviderId);

                        var providerName = (await pplink.TextContentAsync() ?? "").Trim();
                        if (string.IsNullOrWhiteSpace(providerName))
                            providerName = $"Provider {printProviderId}";

                        var providerInfoButton = providerContainer.Locator("button:has(div:has-text('Provider info'))").First;
                        if (await providerInfoButton.CountAsync() == 0)
                        {
                            progress?.Report($"Warning: Provider Info button not found for {providerName}, skipping.");
                            continue;
                        }

                        try
                        {
                            await providerInfoButton.ClickAsync();
                            progress?.Report($"Clicked Provider Info for {providerName}, waiting for modal...");
                        }
                        catch
                        {
                            progress?.Report($"Warning: Could not click Provider Info for {providerName}, skipping.");
                            continue;
                        }

                        try
                        {
                            await page.Locator("[data-testid='modalDialogContent']").WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
                        }
                        catch
                        {
                            progress?.Report($"Warning: Provider Info modal did not appear for {providerName}.");
                            continue;
                        }
                        await page.WaitForTimeoutAsync(1000);

                        progress?.Report($"Extracting colors from {providerName}...");

                        var colorItems = await page.Locator("li[data-testid='color']").AllAsync();
                        var colors = new List<ProviderColor>();

                        foreach (var item in colorItems)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            // Extract background-color from the element
                            var bgColor = await item.EvaluateAsync<string>("el => window.getComputedStyle(el).backgroundColor");
                            var rgb = ParseRgb(bgColor);
                            if (rgb == null)
                            {
                                progress?.Report($"Warning: Could not parse background-color '{bgColor}' for a color item, skipping.");
                                continue;
                            }

                            // Find the first parent with class="text-container"
                            var textContainer = item.Locator("xpath=ancestor::*[contains(@class,'text-container')]").First;
                            var textContainerExists = await textContainer.CountAsync() > 0;
                            if (!textContainerExists)
                            {
                                progress?.Report($"Warning: No parent with class 'text-container' found for color (rgb: {rgb.Value.r},{rgb.Value.g},{rgb.Value.b}), skipping.");
                                continue;
                            }

                            // Find the first child with data-testid="columnText"
                            var columnText = textContainer.Locator("[data-testid='columnText']").First;
                            var columnTextExists = await columnText.CountAsync() > 0;
                            if (!columnTextExists)
                            {
                                progress?.Report($"Warning: No [data-testid='columnText'] found for color (rgb: {rgb.Value.r},{rgb.Value.g},{rgb.Value.b}), skipping.");
                                continue;
                            }

                            var name = (await columnText.TextContentAsync() ?? "").Trim();
                            if (string.IsNullOrEmpty(name))
                            {
                                // Try InnerText as fallback
                                name = (await columnText.InnerTextAsync() ?? "").Trim();
                            }
                            if (string.IsNullOrEmpty(name))
                            {
                                progress?.Report($"Warning: Color name is empty for color (rgb: {rgb.Value.r},{rgb.Value.g},{rgb.Value.b}), skipping.");
                                continue;
                            }

                            colors.Add(new ProviderColor
                            {
                                Name = name,
                                R = rgb.Value.r,
                                G = rgb.Value.g,
                                B = rgb.Value.b,
                            });
                        }

                        providerInfos.Add(new PrintProviderInfo
                        {
                            PrintProviderId = printProviderId,
                            Name = providerName,
                            Colors = colors
                        });

                        // Close the modal
                        try
                        {
                            var closeButton = page.Locator("[data-testid='modalDialogContent'] button[aria-label='Close'], [data-testid='modalDialogContent'] button:has-text('Close')").First;
                            if (await closeButton.CountAsync() > 0)
                                await closeButton.ClickAsync();
                            else
                                await page.Keyboard.PressAsync("Escape");
                        }
                        catch { /* ignore close errors */ }
                        await page.WaitForTimeoutAsync(500);
                    }

                    progress?.Report($"Found {providerInfos.Count} print provider(s).");
                    return providerInfos;
                }
                finally
                {
                    await page.CloseAsync();
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        (int r, int g, int b)? ParseRgb(string cssColor)
        {
            // Parse "rgb(r, g, b)" or "rgba(r, g, b, a)"
            if (string.IsNullOrWhiteSpace(cssColor)) return null;
            var cleaned = cssColor.Replace("rgba", "").Replace("rgb", "").Replace("(", "").Replace(")", "").Trim();
            var parts = cleaned.Split(',');
            if (parts.Length < 3) return null;
            if (int.TryParse(parts[0].Trim(), out var r) &&
                int.TryParse(parts[1].Trim(), out var g) &&
                int.TryParse(parts[2].Trim(), out var b))
            {
                return (r, g, b);
            }
            return null;
        }
    }
}
