using Artsy.Data.Entities;
using Artsy.Data.Interfaces;
using Artsy.API.Services;
using Artsy.PrintifyScraper.Models;
using Artsy.PrintifyScraper.Services;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;

namespace Artsy.API.Hubs
{
    public class PrintifyScraperHub : Hub
    {
        readonly IPrintifyScraperService _scraperService;
        readonly IPrintifyBlueprintRepository _blueprintRepo;
        readonly IPrintifyBlueprintImageRepository _imageRepo;
        readonly IPrintifyBlueprintImageVariantRepository _imageVariantRepo;
        readonly IPrintifyBlueprintVariantRepository _variantRepo;
        readonly IPrintifyBlueprintPrintProviderRepository _printProviderRepo;
        readonly IImageService _imageService;

        public PrintifyScraperHub(
            IPrintifyScraperService scraperService,
            IPrintifyBlueprintRepository blueprintRepo,
            IPrintifyBlueprintImageRepository imageRepo,
            IPrintifyBlueprintImageVariantRepository imageVariantRepo,
            IPrintifyBlueprintVariantRepository variantRepo,
            IPrintifyBlueprintPrintProviderRepository printProviderRepo,
            IImageService imageService)
        {
            _scraperService = scraperService;
            _blueprintRepo = blueprintRepo;
            _imageRepo = imageRepo;
            _imageVariantRepo = imageVariantRepo;
            _variantRepo = variantRepo;
            _printProviderRepo = printProviderRepo;
            _imageService = imageService;
        }

        public async Task StartMatching()
        {
            try
            {
                // Get unpublished blueprints
                var unpublished = await _blueprintRepo.SearchAsync("", "all", 0, 10000, false);
                var blueprintList = unpublished.ToList();

                await SendProgressAsync("init", new
                {
                    total = blueprintList.Count,
                    message = $"Found {blueprintList.Count} unpublished blueprints."
                });

                if (blueprintList.Count == 0)
                {
                    await Clients.Caller.SendAsync("PrintifyScraperComplete", new { success = true, message = "No unpublished blueprints found." });
                    return;
                }

                var processed = 0;
                foreach (var bp in blueprintList)
                {
                    processed++;
                    var connectionId = Context.ConnectionId;

                    try
                    {
                        await SendProgressAsync("blueprint-start", new
                        {
                            blueprintId = bp.BlueprintId,
                            title = bp.Title,
                            brand = bp.Brand,
                            processed,
                            total = blueprintList.Count,
                            message = $"Processing blueprint {processed}/{blueprintList.Count}: {bp.Title}"
                        });

                        // Scrape colors from Printify Provider Info
                        var progress = new Progress<string>(msg =>
                        {
                            _ = Clients.Client(connectionId).SendAsync("PrintifyScraperProgress", new
                            {
                                stage = "scraping",
                                data = new { blueprintId = bp.BlueprintId, message = msg },
                                timestamp = DateTime.UtcNow
                            });
                        });

                        var providerInfos = await _scraperService.ScrapeProviderColorsAsync(
                            bp.BlueprintId, bp.Brand, bp.Title, progress, CancellationToken.None);

                        foreach (var provider in providerInfos)
                        {
                            var colorHexValues = provider.Colors
                                .Where(c => !string.IsNullOrWhiteSpace(c.Name) && !string.IsNullOrWhiteSpace(c.Hex))
                                .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                                .Select(g => (Color: g.Key, HexColor: string.Join(",", g.Select(c => c.Hex.TrimStart('#')).Distinct())))
                                .Where(x => !string.IsNullOrWhiteSpace(x.HexColor))
                                .ToList();
                            if (colorHexValues.Count > 0)
                                await _variantRepo.UpdateHexColorsAsync(bp.BlueprintId, provider.PrintProviderId, colorHexValues);
                        }

                        // Get variant colors from database
                        var variants = await _variantRepo.GetByBlueprintIdAsync(bp.BlueprintId);
                        var variantColors = variants
                            .Select(v => v.Color)
                            .Where(c => !string.IsNullOrWhiteSpace(c))
                            .Distinct()
                            .ToList();

                        var providerCount = providerInfos.Count;
                        var totalColorCount = providerInfos.Sum(p => p.Colors.Count);

                        if (totalColorCount == 0)
                        {
                            // No colors extracted from Provider Info — fall back to variant colors from DB
                            var printProviders = await _printProviderRepo.GetByBlueprintIdAsync(bp.BlueprintId);
                            var providerNames = printProviders.ToDictionary(p => p.PrintProviderId, p => p.Title);

                            string? GetColorFromOptions(string? optionsJson)
                            {
                                if (string.IsNullOrWhiteSpace(optionsJson)) return null;
                                try
                                {
                                    using var doc = JsonDocument.Parse(optionsJson);
                                    if (doc.RootElement.TryGetProperty("color", out var c) && c.ValueKind == JsonValueKind.String)
                                        return c.GetString();
                                    if (doc.RootElement.TryGetProperty("finish", out var f) && f.ValueKind == JsonValueKind.String)
                                        return f.GetString();
                                }
                                catch { }
                                return null;
                            }

                            var providerColorMap = new Dictionary<int, List<ProviderColor>>();
                            foreach (var v in variants)
                            {
                                var colorName = !string.IsNullOrWhiteSpace(v.Color)
                                    ? v.Color
                                    : (GetColorFromOptions(v.Options) ?? "");
                                if (string.IsNullOrWhiteSpace(colorName)) continue;

                                if (!providerColorMap.ContainsKey(v.PrintProviderId))
                                    providerColorMap[v.PrintProviderId] = new List<ProviderColor>();

                                // Parse HexColor into RGB if available, otherwise use -1
                                int r = -1, g = -1, b = -1;
                                if (!string.IsNullOrWhiteSpace(v.HexColor) && v.HexColor.Length == 6 &&
                                    int.TryParse(v.HexColor.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out r) &&
                                    int.TryParse(v.HexColor.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out g) &&
                                    int.TryParse(v.HexColor.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out b))
                                { }
                                else
                                {
                                    r = -1; g = -1; b = -1;
                                }

                                providerColorMap[v.PrintProviderId].Add(new ProviderColor { Name = colorName, R = r, G = g, B = b });
                            }

                            providerInfos = providerColorMap
                                .Select(kvp => new PrintProviderInfo
                                {
                                    PrintProviderId = kvp.Key,
                                    Name = providerNames.TryGetValue(kvp.Key, out var title) && !string.IsNullOrWhiteSpace(title) ? title : $"Provider {kvp.Key}",
                                    Colors = kvp.Value.GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList()
                                })
                                .ToList();

                            if (providerInfos.Count == 0)
                            {
                                // No variant colors in DB either — this is an error
                                var printifyUrl = $"https://printify.com/app/products/{bp.BlueprintId}/{Slugify(bp.Brand)}/{Slugify(bp.Title)}";
                                await SendProgressAsync("blueprint-error", new
                                {
                                    blueprintId = bp.BlueprintId,
                                    title = bp.Title,
                                    message = "No colors found in Provider Info and no variant colors exist in the database.",
                                    url = printifyUrl
                                });
                                await WaitForSkip(connectionId, bp.BlueprintId);
                                continue;
                            }

                            await SendProgressAsync("colors-extracted", new
                            {
                                blueprintId = bp.BlueprintId,
                                providers = providerInfos.Select(p => new
                                {
                                    printProviderId = p.PrintProviderId,
                                    name = p.Name,
                                    colors = p.Colors.Select(c => new { name = c.Name, r = c.R, g = c.G, b = c.B, hex = c.Hex })
                                }),
                                message = $"No colors found in Provider Info. Using {providerInfos.Sum(p => p.Colors.Count)} variant colors from database."
                            });
                        }
                        else
                        {
                            await SendProgressAsync("colors-extracted", new
                            {
                                blueprintId = bp.BlueprintId,
                                providers = providerInfos.Select(p => new
                                {
                                    printProviderId = p.PrintProviderId,
                                    name = p.Name,
                                    colors = p.Colors.Select(c => new { name = c.Name, r = c.R, g = c.G, b = c.B, hex = c.Hex })
                                }),
                                message = $"Extracted {totalColorCount} color(s) from {providerCount} print provider(s)."
                            });
                        }

                        // Get existing images for this blueprint and build a complete list up to blueprint image count
                        var images = await _imageRepo.GetByBlueprintIdAsync(bp.BlueprintId);
                        var dbImagesByIndex = images.ToDictionary(img => img.ImageIndex);
                        var imageList = new List<PrintifyBlueprintImage>();
                        for (int i = 0; i < bp.ImageCount; i++)
                        {
                            imageList.Add(dbImagesByIndex.TryGetValue(i, out var dbImg)
                                ? dbImg
                                : new PrintifyBlueprintImage { BlueprintId = bp.BlueprintId, ImageIndex = i });
                        }

                        // Process each image
                        for (int idx = 0; idx < imageList.Count; idx++)
                        {
                            var img = imageList[idx];
                            byte[]? imageBytes = null;
                            try
                            {
                                imageBytes = await _imageService.GetPrintifyCatalogImageAsync(bp.BlueprintId, img.ImageIndex);
                            }
                            catch { /* image may not exist */ }

                            if (imageBytes == null || imageBytes.Length == 0)
                            {
                                await SendProgressAsync("image-skip", new
                                {
                                    blueprintId = bp.BlueprintId,
                                    imageIndex = img.ImageIndex,
                                    message = $"Image {img.ImageIndex} not found, skipping."
                                });
                                continue;
                            }

                            // Send image + all colors to client for selection
                            var base64Image = Convert.ToBase64String(imageBytes);

                            await SendProgressAsync("image-prompt", new
                            {
                                blueprintId = bp.BlueprintId,
                                blueprintTitle = bp.Title,
                                printifyUrl = $"https://printify.com/app/products/{bp.BlueprintId}/{Slugify(bp.Brand)}/{Slugify(bp.Title)}",
                                imageIndex = img.ImageIndex,
                                imageCount = imageList.Count,
                                imageBase64 = $"data:image/jpeg;base64,{base64Image}",
                                type = img.Type,
                                position = img.Position,
                                providers = providerInfos.Select(p => new
                                {
                                    printProviderId = p.PrintProviderId,
                                    name = p.Name,
                                    colors = p.Colors.Select(c => new { name = c.Name, r = c.R, g = c.G, b = c.B, hex = c.Hex })
                                }),
                                variantColors,
                                message = $"Image {img.ImageIndex + 1}/{imageList.Count}: Select colors to apply."
                            });

                            // Wait for user to click Apply Variants
                            var (selectedColors, position, type, goBack) = await WaitForApplyVariants(connectionId, bp.BlueprintId, img.ImageIndex);

                            // Ensure the image record exists in DB (with selected type and position)
                            Guid imageId = img.Id;
                            if (imageId == Guid.Empty)
                            {
                                imageId = await _imageRepo.UpsertAsync(new PrintifyBlueprintImage
                                {
                                    BlueprintId = bp.BlueprintId,
                                    ImageIndex = img.ImageIndex,
                                    Type = type,
                                    Position = position,
                                });
                            }
                            else if (img.Position != position || img.Type != type)
                            {
                                imageId = await _imageRepo.UpsertAsync(new PrintifyBlueprintImage
                                {
                                    Id = img.Id,
                                    BlueprintId = bp.BlueprintId,
                                    ImageIndex = img.ImageIndex,
                                    Type = type,
                                    Position = position,
                                });
                            }

                            // Delete existing variants and replace with selected colors
                            await _imageVariantRepo.DeleteByBlueprintImageIdAsync(imageId);
                            if (selectedColors.Count > 0)
                            {
                                await _imageVariantRepo.UpsertAsync(imageId, selectedColors);
                            }

                            await SendProgressAsync("image-complete", new
                            {
                                blueprintId = bp.BlueprintId,
                                imageIndex = img.ImageIndex,
                                appliedCount = selectedColors.Count,
                                message = $"Image {img.ImageIndex}: {selectedColors.Count} colors applied."
                            });

                            // Navigate back to the previous image if requested
                            if (goBack && idx > 0)
                            {
                                idx -= 2;
                            }
                        }

                        // Publish the blueprint
                        await _blueprintRepo.UpdatePublishedAsync(bp.BlueprintId, true);

                        await SendProgressAsync("blueprint-complete", new
                        {
                            blueprintId = bp.BlueprintId,
                            processed,
                            total = blueprintList.Count,
                            message = $"Blueprint \"{bp.Title}\" published."
                        });
                    }
                    catch (Exception ex)
                    {
                        // Send error to client and wait for user to click Skip
                        var printifyUrl = $"https://printify.com/app/products/{bp.BlueprintId}/{Slugify(bp.Brand)}/{Slugify(bp.Title)}";
                        await SendProgressAsync("blueprint-error", new
                        {
                            blueprintId = bp.BlueprintId,
                            title = bp.Title,
                            message = $"Error: {ex.Message}",
                            url = printifyUrl
                        });
                        await WaitForSkip(connectionId, bp.BlueprintId);
                    }
                }

                await SendProgressAsync("complete", new { message = $"Done! Processed {processed} blueprints." });
                await Clients.Caller.SendAsync("PrintifyScraperComplete", new { success = true });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("PrintifyScraperComplete", new { success = false, message = ex.Message });
            }
        }

        // Called by client when user clicks Apply Variants
        public async Task ApplyVariants(int blueprintId, int imageIndex, string[] selectedColors, int position, int type = 0, bool goBack = false)
        {
            var key = $"{Context.ConnectionId}:{blueprintId}:{imageIndex}";
            _applyResults[key] = selectedColors?.ToList() ?? new List<string>();
            _applyPositions[key] = position;
            _applyTypes[key] = type;
            _applyGoBack[key] = goBack;
            if (_applyTcs.TryGetValue(key, out var tcs))
                tcs.TrySetResult(true);
        }

        // Called by client when user clicks Skip on a blueprint error
        public Task SkipBlueprint(int blueprintId)
        {
            var key = $"{Context.ConnectionId}:{blueprintId}";
            if (_skipTcs.TryGetValue(key, out var tcs))
                tcs.TrySetResult(true);
            return Task.CompletedTask;
        }

        async Task WaitForSkip(string connectionId, int blueprintId)
        {
            var key = $"{connectionId}:{blueprintId}";
            var tcs = new TaskCompletionSource<bool>();
            _skipTcs[key] = tcs;

            // Wait with timeout (10 minutes)
            await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromMinutes(10)));

            _skipTcs.Remove(key);
        }

        async Task<(List<string> colors, int position, int type, bool goBack)> WaitForApplyVariants(string connectionId, int blueprintId, int imageIndex)
        {
            var key = $"{connectionId}:{blueprintId}:{imageIndex}";
            var tcs = new TaskCompletionSource<bool>();
            _applyTcs[key] = tcs;

            // Wait with timeout (10 minutes per image)
            await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromMinutes(10)));

            var colors = _applyResults.TryGetValue(key, out var c) ? c : new List<string>();
            var position = _applyPositions.TryGetValue(key, out var p) ? p : 1;

            var goBack = _applyGoBack.TryGetValue(key, out var gb) && gb;
            var type = _applyTypes.TryGetValue(key, out var t) ? t : 0;

            _applyTcs.Remove(key);
            _applyResults.Remove(key);
            _applyPositions.Remove(key);
            _applyTypes.Remove(key);
            _applyGoBack.Remove(key);

            return (colors, position, type, goBack);
        }

        async Task SendProgressAsync(string stage, object data)
        {
            await Clients.Caller.SendAsync("PrintifyScraperProgress", new
            {
                stage,
                data,
                timestamp = DateTime.UtcNow
            });
        }

        static string Slugify(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            var slug = text.ToLowerInvariant();
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"&", "and");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"\+", "plus");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9]+", "-");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"^-+|-+$", "");
            return slug;
        }

        static readonly Dictionary<string, TaskCompletionSource<bool>> _applyTcs = new();
        static readonly Dictionary<string, List<string>> _applyResults = new();
        static readonly Dictionary<string, int> _applyPositions = new();
        static readonly Dictionary<string, int> _applyTypes = new();
        static readonly Dictionary<string, bool> _applyGoBack = new();
        static readonly Dictionary<string, TaskCompletionSource<bool>> _skipTcs = new();
    }
}
