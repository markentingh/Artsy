using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Artsy.API.Models;
using Artsy.API.Services;
using Artsy.Auth.Policies;
using Artsy.Data.Entities;
using Artsy.Data.Interfaces;

namespace Artsy.API.Controllers.Admin
{
    [Route("/api/admin/printify")]
    [Authorize(Policy = nameof(AuthConstants.Policy.ManageUsers))]
    public class PrintifyController : ApiController
    {
        readonly IPrintifyBlueprintRepository _printifyBlueprintRepo;
        readonly IPrintifyBlueprintPrintProviderRepository _printProviderRepo;
        readonly IPrintifyBlueprintVariantRepository _variantRepo;
        readonly IPrintifyBlueprintVariantPlaceholderRepository _placeholderRepo;
        readonly IPrintifyBlueprintShippingRepository _shippingRepo;
        readonly IHttpClientFactory _httpClientFactory;
        readonly IImageService _imageService;

        public PrintifyController(
            IPrintifyBlueprintRepository printifyBlueprintRepo,
            IPrintifyBlueprintPrintProviderRepository printProviderRepo,
            IPrintifyBlueprintVariantRepository variantRepo,
            IPrintifyBlueprintVariantPlaceholderRepository placeholderRepo,
            IPrintifyBlueprintShippingRepository shippingRepo,
            IHttpClientFactory httpClientFactory,
            IImageService imageService)
        {
            _printifyBlueprintRepo = printifyBlueprintRepo;
            _printProviderRepo = printProviderRepo;
            _variantRepo = variantRepo;
            _placeholderRepo = placeholderRepo;
            _shippingRepo = shippingRepo;
            _httpClientFactory = httpClientFactory;
            _imageService = imageService;
        }

        private HttpClient CreatePrintifyClient()
        {
            var client = _httpClientFactory.CreateClient();
            var token = ConnectionSettings.PrintifyApiToken;
            if (!string.IsNullOrEmpty(token))
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        [HttpGet("catalog-count")]
        public async Task<IActionResult> GetCatalogCount()
        {
            try
            {
                var count = await _printifyBlueprintRepo.GetCountAsync();
                return Json(new ApiResponse { success = true, data = new { count } });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("refresh-catalog")]
        public async Task<IActionResult> RefreshCatalog()
        {
            try
            {
                using var client = CreatePrintifyClient();
                var response = await client.GetAsync("https://api.printify.com/v1/catalog/blueprints.json");
                if (!response.IsSuccessStatusCode)
                    return Json(new ApiResponse { success = false, message = $"Printify API error: {response.StatusCode}" });

                var json = await response.Content.ReadAsStringAsync();
                var blueprints = JsonSerializer.Deserialize<List<JsonElement>>(json) ?? [];

                var bpEntities = new List<PrintifyBlueprint>();
                var blueprintIds = new List<int>();
                var imagesToDownload = new List<object>();

                foreach (var bp in blueprints)
                {
                    var blueprintId = bp.TryGetProperty("id", out var id) ? id.GetInt32() : 0;
                    var images = bp.TryGetProperty("images", out var imgEl)
                        ? imgEl.EnumerateArray().Select(i => i.GetString() ?? "").ToArray()
                        : Array.Empty<string>();

                    bpEntities.Add(new PrintifyBlueprint
                    {
                        BlueprintId = blueprintId,
                        Title = bp.TryGetProperty("title", out var title) ? title.GetString() ?? "" : "",
                        Description = bp.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
                        Brand = bp.TryGetProperty("brand", out var brand) ? brand.GetString() ?? "" : "",
                        Model = bp.TryGetProperty("model", out var model) ? model.GetString() ?? "" : "",
                        ImageCount = images.Length
                    });

                    blueprintIds.Add(blueprintId);

                    for (int i = 0; i < images.Length; i++)
                    {
                        if (!string.IsNullOrEmpty(images[i]))
                            imagesToDownload.Add(new { blueprintId, index = i, url = images[i] });
                    }
                }

                await _printifyBlueprintRepo.UpsertBatchAsync(bpEntities);

                var existingBlueprintIds = await _printifyBlueprintRepo.GetAllBlueprintIdsAsync();
                var existingSet = new HashSet<int>(existingBlueprintIds);
                var newBlueprints = blueprintIds.Where(id => !existingSet.Contains(id)).ToList();

                return Json(new ApiResponse { success = true, data = new { count = bpEntities.Count, blueprints = newBlueprints, images = imagesToDownload } });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("fetch-print-providers")]
        public async Task<IActionResult> FetchPrintProviders([FromBody] JsonElement body)
        {
            try
            {
                var blueprintId = body.TryGetProperty("blueprintId", out var bpId) ? bpId.GetInt32() : 0;
                if (blueprintId == 0)
                    return Json(new ApiResponse { success = false, message = "Missing blueprintId" });

                using var client = CreatePrintifyClient();
                var response = await client.GetAsync($"https://api.printify.com/v1/catalog/blueprints/{blueprintId}/print_providers.json");
                if (!response.IsSuccessStatusCode)
                    return Json(new ApiResponse { success = false, message = $"Printify API error: {response.StatusCode}" });

                var json = await response.Content.ReadAsStringAsync();
                var ppList = JsonSerializer.Deserialize<List<JsonElement>>(json) ?? [];

                var ppEntities = new List<PrintifyBlueprintPrintProvider>();
                var providers = new List<object>();

                foreach (var pp in ppList)
                {
                    var providerId = pp.TryGetProperty("id", out var pid) ? pid.GetInt32() : 0;
                    var decorationMethods = pp.TryGetProperty("decoration_methods", out var dm)
                        ? JsonSerializer.Serialize(dm.EnumerateArray().Select(d => d.GetString() ?? "").ToArray())
                        : "[]";

                    ppEntities.Add(new PrintifyBlueprintPrintProvider
                    {
                        BlueprintId = blueprintId,
                        PrintProviderId = providerId,
                        Title = pp.TryGetProperty("title", out var pt) ? pt.GetString() ?? "" : "",
                        DecorationMethods = decorationMethods
                    });

                    providers.Add(new { blueprintId, printProviderId = providerId });
                }

                if (ppEntities.Count > 0) await _printProviderRepo.UpsertBatchAsync(ppEntities);

                return Json(new ApiResponse { success = true, data = new { printProviders = providers } });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("fetch-variants")]
        public async Task<IActionResult> FetchVariants([FromBody] JsonElement body)
        {
            try
            {
                var blueprintId = body.TryGetProperty("blueprintId", out var bpId) ? bpId.GetInt32() : 0;
                var printProviderId = body.TryGetProperty("printProviderId", out var ppId) ? ppId.GetInt32() : 0;

                if (blueprintId == 0 || printProviderId == 0)
                    return Json(new ApiResponse { success = false, message = "Missing blueprintId or printProviderId" });

                using var client = CreatePrintifyClient();
                var response = await client.GetAsync($"https://api.printify.com/v1/catalog/blueprints/{blueprintId}/print_providers/{printProviderId}/variants.json");
                if (!response.IsSuccessStatusCode)
                    return Json(new ApiResponse { success = false, message = $"Printify API error: {response.StatusCode}" });

                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonSerializer.Deserialize<JsonElement>(json);

                var variantEntities = new List<PrintifyBlueprintVariant>();
                var placeholderEntities = new List<PrintifyBlueprintVariantPlaceholder>();

                if (doc.TryGetProperty("variants", out var vArr))
                {
                    foreach (var v in vArr.EnumerateArray())
                    {
                        var variantId = v.TryGetProperty("id", out var vid) ? vid.GetInt32() : 0;
                        var options = v.TryGetProperty("options", out var opt) ? JsonSerializer.Serialize(opt) : "{}";
                        var vDecorationMethods = v.TryGetProperty("decoration_methods", out var vdm)
                            ? JsonSerializer.Serialize(vdm.EnumerateArray().Select(d => d.GetString() ?? "").ToArray())
                            : "[]";

                        variantEntities.Add(new PrintifyBlueprintVariant
                        {
                            VariantId = variantId,
                            BlueprintId = blueprintId,
                            PrintProviderId = printProviderId,
                            Title = v.TryGetProperty("title", out var vt) ? vt.GetString() ?? "" : "",
                            Options = options,
                            DecorationMethods = vDecorationMethods
                        });

                        if (v.TryGetProperty("placeholders", out var phArr))
                        {
                            foreach (var ph in phArr.EnumerateArray())
                            {
                                placeholderEntities.Add(new PrintifyBlueprintVariantPlaceholder
                                {
                                    VariantId = variantId,
                                    Position = ph.TryGetProperty("position", out var pos) ? pos.GetString() ?? "" : "",
                                    DecorationMethod = ph.TryGetProperty("decoration_method", out var dmethod) ? dmethod.GetString() ?? "" : "",
                                    Height = ph.TryGetProperty("height", out var h) ? h.GetInt32() : 0,
                                    Width = ph.TryGetProperty("width", out var w) ? w.GetInt32() : 0
                                });
                            }
                        }
                    }
                }

                if (variantEntities.Count > 0) await _variantRepo.UpsertBatchAsync(variantEntities);
                if (placeholderEntities.Count > 0) await _placeholderRepo.UpsertBatchAsync(placeholderEntities);

                return Json(new ApiResponse { success = true, data = new { variants = variantEntities.Count, placeholders = placeholderEntities.Count } });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("fetch-shipping")]
        public async Task<IActionResult> FetchShipping([FromBody] JsonElement body)
        {
            try
            {
                var blueprintId = body.TryGetProperty("blueprintId", out var bpId) ? bpId.GetInt32() : 0;
                var printProviderId = body.TryGetProperty("printProviderId", out var ppId) ? ppId.GetInt32() : 0;

                if (blueprintId == 0 || printProviderId == 0)
                    return Json(new ApiResponse { success = false, message = "Missing blueprintId or printProviderId" });

                using var client = CreatePrintifyClient();
                var response = await client.GetAsync($"https://api.printify.com/v1/catalog/blueprints/{blueprintId}/print_providers/{printProviderId}/shipping.json");
                if (!response.IsSuccessStatusCode)
                    return Json(new ApiResponse { success = false, message = $"Printify API error: {response.StatusCode}" });

                var json = await response.Content.ReadAsStringAsync();
                var sDoc = JsonSerializer.Deserialize<JsonElement>(json);

                var handlingTimeValue = 0;
                var handlingTimeUnit = "day";
                if (sDoc.TryGetProperty("handling_time", out var ht))
                {
                    if (ht.TryGetProperty("value", out var htv)) handlingTimeValue = htv.GetInt32();
                    if (ht.TryGetProperty("unit", out var htu)) handlingTimeUnit = htu.GetString() ?? "day";
                }

                var profiles = "[]";
                if (sDoc.TryGetProperty("profiles", out var profEl))
                    profiles = JsonSerializer.Serialize(profEl);

                await _shippingRepo.UpsertAsync(new PrintifyBlueprintShipping
                {
                    BlueprintId = blueprintId,
                    PrintProviderId = printProviderId,
                    HandlingTimeValue = handlingTimeValue,
                    HandlingTimeUnit = handlingTimeUnit,
                    Profiles = profiles
                });

                return Json(new ApiResponse { success = true, data = new { saved = true } });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("download-catalog-image")]
        public async Task<IActionResult> DownloadCatalogImage([FromBody] JsonElement body)
        {
            try
            {
                var blueprintId = body.TryGetProperty("blueprintId", out var bpId) ? bpId.GetInt32() : 0;
                var index = body.TryGetProperty("index", out var idx) ? idx.GetInt32() : 0;
                var url = body.TryGetProperty("url", out var urlEl) ? urlEl.GetString() ?? "" : "";

                if (blueprintId == 0 || string.IsNullOrEmpty(url))
                    return Json(new ApiResponse { success = false, message = "Missing blueprintId or url" });

                var existing = await _imageService.GetPrintifyCatalogImageAsync(blueprintId, index);
                if (existing.Length > 0)
                    return Json(new ApiResponse { success = true, data = new { downloaded = false } });

                using var client = _httpClientFactory.CreateClient();
                var imageBytes = await client.GetByteArrayAsync(url);
                await _imageService.SavePrintifyCatalogImageAsync(blueprintId, index, imageBytes);

                return Json(new ApiResponse { success = true, data = new { downloaded = true } });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("blueprints")]
        public async Task<IActionResult> SearchBlueprints([FromQuery] string? keyword, [FromQuery] string? brand, [FromQuery] int start = 0, [FromQuery] int length = 20)
        {
            try
            {
                var kw = keyword ?? "";
                var br = brand ?? "all";
                var results = await _printifyBlueprintRepo.SearchAsync(kw, br, start, length);
                var total = await _printifyBlueprintRepo.GetCountAsync(kw, br);

                return Json(new ApiResponse
                {
                    success = true,
                    data = new
                    {
                        blueprints = results.Select(bp => new
                        {
                            id = bp.BlueprintId,
                            title = bp.Title,
                            brand = bp.Brand,
                            model = bp.Model,
                            description = bp.Description,
                            imageCount = bp.ImageCount
                        }),
                        total,
                        hasMore = (start + length) < total
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("brands")]
        public async Task<IActionResult> GetBrands()
        {
            try
            {
                var brands = await _printifyBlueprintRepo.GetBrandsAsync();
                return Json(new ApiResponse { success = true, data = new { brands } });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("blueprint-image")]
        public async Task<IActionResult> GetBlueprintImage([FromQuery] int blueprintId, [FromQuery] int index = 0, [FromQuery] bool thumb = false)
        {
            try
            {
                var bytes = await _imageService.GetPrintifyCatalogImageAsync(blueprintId, index, thumb);
                if (bytes.Length == 0)
                    return NotFound();
                return File(bytes, "image/jpeg");
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("blueprints/{blueprintId}")]
        public async Task<IActionResult> GetBlueprintDetail(int blueprintId)
        {
            try
            {
                var cached = await _printifyBlueprintRepo.GetByBlueprintIdAsync(blueprintId);
                if (cached == null)
                    return Json(new ApiResponse { success = false, message = "Blueprint not found in catalog" });

                var providers = await _printProviderRepo.GetByBlueprintIdAsync(blueprintId);
                var printProviders = providers.Select(pp => new
                {
                    id = pp.PrintProviderId,
                    title = pp.Title,
                    decoration_methods = JsonSerializer.Deserialize<string[]>(pp.DecorationMethods) ?? Array.Empty<string>()
                }).ToList();

                return Json(new ApiResponse
                {
                    success = true,
                    data = new
                    {
                        blueprint = new
                        {
                            id = cached.BlueprintId,
                            title = cached.Title,
                            brand = cached.Brand,
                            model = cached.Model,
                            description = cached.Description,
                            imageCount = cached.ImageCount
                        },
                        printProviders
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("blueprints/{blueprintId}/print-providers/{printProviderId}/variants")]
        public async Task<IActionResult> GetBlueprintVariants(int blueprintId, int printProviderId)
        {
            try
            {
                var cachedVariants = await _variantRepo.GetByBlueprintAndProviderAsync(blueprintId, printProviderId);

                var variantIds = cachedVariants.Select(v => v.VariantId).ToList();
                var allPlaceholders = new Dictionary<int, List<object>>();
                if (variantIds.Count > 0)
                {
                    foreach (var vid in variantIds)
                    {
                        var phs = await _placeholderRepo.GetByVariantIdAsync(vid);
                        allPlaceholders[vid] = phs.Select(ph => (object)new
                        {
                            position = ph.Position,
                            decoration_method = ph.DecorationMethod,
                            height = ph.Height,
                            width = ph.Width
                        }).ToList();
                    }
                }

                var variants = cachedVariants.Select(v => new
                {
                    id = v.VariantId,
                    title = v.Title,
                    options = JsonSerializer.Deserialize<Dictionary<string, string>>(v.Options) ?? new Dictionary<string, string>(),
                    placeholders = allPlaceholders.TryGetValue(v.VariantId, out var phs) ? phs : new List<object>(),
                    decoration_methods = JsonSerializer.Deserialize<string[]>(v.DecorationMethods) ?? Array.Empty<string>()
                }).ToList();

                return Json(new ApiResponse { success = true, data = new { variants } });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }
    }
}
