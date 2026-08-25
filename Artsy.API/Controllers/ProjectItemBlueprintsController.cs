using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.RegularExpressions;
using Artsy.API.Models;
using Artsy.API.Models.Projects;
using Artsy.AI;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.API.Controllers
{
    [Authorize]
    public partial class ProjectsController
    {
        private static bool IsBlueprintConfigured(string name, string description, string blueprintJson, string placementJson, string pricingJson, IEnumerable<ProjectBlueprintProductImage>? productImages = null)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            if (string.IsNullOrWhiteSpace(description)) return false;
            if (string.IsNullOrWhiteSpace(blueprintJson)) return false;
            if (string.IsNullOrWhiteSpace(placementJson)) return false;
            try
            {
                var cfg = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(blueprintJson);
                if (cfg == null || !cfg.TryGetValue("variantIds", out var variantEl) || variantEl.ValueKind != JsonValueKind.Array)
                    return false;
                var variantIds = variantEl.EnumerateArray().Select(v => v.GetInt32()).ToList();
                if (variantIds.Count == 0) return false;

                var pricing = string.IsNullOrWhiteSpace(pricingJson) ? new List<JsonElement>() : JsonSerializer.Deserialize<List<JsonElement>>(pricingJson) ?? new List<JsonElement>();
                var priceMap = new Dictionary<int, decimal>();
                foreach (var p in pricing)
                {
                    if (p.TryGetProperty("variantId", out var vidEl) && p.TryGetProperty("price", out var priceEl))
                        priceMap[vidEl.GetInt32()] = priceEl.GetDecimal();
                }
                if (!variantIds.All(vid => priceMap.TryGetValue(vid, out var price) && price > 0))
                    return false;

                var placements = JsonSerializer.Deserialize<List<JsonElement>>(placementJson);
                if (placements == null || placements.Count == 0) return false;
                var hasPlacements = placements.Any(p =>
                {
                    if (!p.TryGetProperty("source", out var srcEl)) return false;
                    var source = srcEl.GetString() ?? "";
                    if (string.IsNullOrWhiteSpace(source)) return false;
                    if (source == "item" && p.TryGetProperty("itemId", out var itemEl) && itemEl.ValueKind != JsonValueKind.Null)
                        return true;
                    if (source == "custom" && p.TryGetProperty("customImageId", out var imgEl) && imgEl.ValueKind != JsonValueKind.Null)
                        return true;
                    return false;
                });
                if (!hasPlacements) return false;

                if (productImages == null || !productImages.Any())
                    return false;

                return productImages.All(img => !string.IsNullOrWhiteSpace(img.Prompt));
            }
            catch { return false; }
        }

        private static decimal? GetMinPriceFromPricingJson(string pricingJson)
        {
            if (string.IsNullOrWhiteSpace(pricingJson)) return null;
            try
            {
                var pricing = JsonSerializer.Deserialize<List<JsonElement>>(pricingJson);
                if (pricing == null || pricing.Count == 0) return null;
                decimal? min = null;
                foreach (var p in pricing)
                {
                    if (p.TryGetProperty("price", out var priceEl))
                    {
                        var price = priceEl.GetDecimal();
                        if (price > 0 && (min == null || price < min))
                            min = price;
                    }
                }
                return min;
            }
            catch { return null; }
        }

        [HttpGet("get-blueprints")]
        public async Task<IActionResult> GetBlueprints([FromQuery] Guid projectId)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (projectId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Project ID is required." });

            try
            {
                var project = await _projectRepository.GetByIdAsync(projectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                var blueprints = await _projectBlueprintRepository.GetListByProjectIdAsync(projectId);
                var blueprintIds = blueprints.Select(b => b.Id).ToList();
                var productImages = await _projectBlueprintProductImageRepository.GetByBlueprintIdsAsync(blueprintIds);
                var imagesByBlueprint = productImages.GroupBy(img => img.ProjectBlueprintId).ToDictionary(g => g.Key, g => g.ToList());
                
                var printifyBlueprintIds = blueprints.Select(b => b.BlueprintId).Distinct().ToList();
                var printifyImages = await _printifyBlueprintImageRepository.GetByBlueprintIdsAsync(printifyBlueprintIds);
                var printifyImagesByBlueprint = printifyImages.GroupBy(img => img.BlueprintId).ToDictionary(g => g.Key, g => g.ToList());
                var printifyImageVariants = await _printifyBlueprintImageVariantRepository.GetByBlueprintImageIdsAsync(printifyImages.Select(img => img.Id));
                var printifyImageVariantsByImageId = printifyImageVariants.GroupBy(v => v.BlueprintImageId).ToDictionary(g => g.Key, g => g.Select(v => v.VariantColor).ToList());

                foreach (var b in blueprints)
                {
                    var bpImages = imagesByBlueprint.TryGetValue(b.Id, out var imgs) ? imgs : null;
                    b.Configured = IsBlueprintConfigured(b.Name, b.Description, b.BlueprintJson, b.PlacementJson, b.PricingJson, bpImages);
                }
                return Json(new ApiResponse { success = true, data = blueprints.Select(b => new {
                    b.Id, b.BlueprintId, b.Name, b.BlueprintJson, b.PlacementJson, b.Prompt, b.Description, b.SafetyInfo, b.PricingJson, b.PrintProviderId, b.Configured, b.ImageCount,
                    minPrice = GetMinPriceFromPricingJson(b.PricingJson),
                    printifyImages = printifyImagesByBlueprint.TryGetValue(b.BlueprintId, out var pImgs) ? pImgs.Select(img => new { variantColors = printifyImageVariantsByImageId.TryGetValue(img.Id, out var colors) ? colors : new List<string>(), img.ImageIndex }).Cast<object>() : Enumerable.Empty<object>()
                }) });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("get-blueprints-list")]
        public async Task<IActionResult> GetBlueprintsList([FromQuery] Guid projectId)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (projectId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Project ID is required." });

            try
            {
                var project = await _projectRepository.GetByIdAsync(projectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                var blueprints = await _projectBlueprintRepository.GetListByProjectIdAsync(projectId);
                var blueprintIds = blueprints.Select(b => b.Id).ToList();
                var productImages = await _projectBlueprintProductImageRepository.GetByBlueprintIdsAsync(blueprintIds);
                var imagesByBlueprint = productImages.GroupBy(img => img.ProjectBlueprintId).ToDictionary(g => g.Key, g => g.ToList());
                var result = blueprints.Select(b => new ProjectBlueprintListResponse
                {
                    Id = b.Id,
                    BlueprintId = b.BlueprintId,
                    Name = b.Name,
                    BlueprintJson = b.BlueprintJson,
                    Configured = IsBlueprintConfigured(b.Name, b.Description, b.BlueprintJson, b.PlacementJson, b.PricingJson, imagesByBlueprint.TryGetValue(b.Id, out var imgs) ? imgs : null),
                    ImageCount = b.ImageCount,
                    MinPrice = GetMinPriceFromPricingJson(b.PricingJson)
                });
                return Json(new ApiResponse { success = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("create-blueprint")]
        public async Task<IActionResult> CreateBlueprint([FromBody] CreateProjectBlueprintRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.ProjectId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Project ID is required." });

            if (string.IsNullOrWhiteSpace(request.Name))
                return Json(new ApiResponse { success = false, message = "Name is required." });

            try
            {
                var project = await _projectRepository.GetByIdAsync(request.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                var blueprint = new ProjectBlueprints
                {
                    ProjectId = request.ProjectId,
                    BlueprintId = request.BlueprintId,
                    Name = request.Name.Trim(),
                    BlueprintJson = request.BlueprintJson ?? "",
                    PlacementJson = request.PlacementJson ?? "",
                    Prompt = request.Prompt ?? "",
                    Description = request.Description ?? "",
                    SafetyInfo = request.SafetyInfo ?? "",
                    PricingJson = request.PricingJson ?? "[]",
                    PrintProviderId = request.PrintProviderId
                };
                var created = await _projectBlueprintRepository.CreateAsync(blueprint);
                return Json(new ApiResponse { success = true, data = created });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("delete-blueprint")]
        public async Task<IActionResult> DeleteBlueprint([FromBody] DeleteProjectBlueprintRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.Id == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Blueprint ID is required." });

            try
            {
                var blueprint = await _projectBlueprintRepository.GetByIdAsync(request.Id);
                if (blueprint == null)
                    return Json(new ApiResponse { success = false, message = "Blueprint not found." });

                var project = await _projectRepository.GetByIdAsync(blueprint.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                await _projectBlueprintRepository.DeleteAsync(request.Id);
                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("update-blueprint")]
        public async Task<IActionResult> UpdateBlueprint([FromBody] UpdateProjectBlueprintRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.Id == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Blueprint ID is required." });

            if (string.IsNullOrWhiteSpace(request.Name))
                return Json(new ApiResponse { success = false, message = "Name is required." });

            try
            {
                var blueprint = await _projectBlueprintRepository.GetByIdAsync(request.Id);
                if (blueprint == null)
                    return Json(new ApiResponse { success = false, message = "Blueprint not found." });

                var project = await _projectRepository.GetByIdAsync(blueprint.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                blueprint.BlueprintId = request.BlueprintId;
                blueprint.Name = request.Name.Trim();
                blueprint.BlueprintJson = request.BlueprintJson ?? "";
                blueprint.PlacementJson = request.PlacementJson ?? "";
                blueprint.Prompt = request.Prompt ?? "";
                blueprint.Description = request.Description ?? "";
                blueprint.SafetyInfo = request.SafetyInfo ?? "";
                blueprint.PricingJson = request.PricingJson ?? "[]";
                blueprint.PrintProviderId = request.PrintProviderId;
                await _projectBlueprintRepository.UpdateAsync(blueprint);
                return Json(new ApiResponse { success = true, data = blueprint });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("get-blueprint-placeholders")]
        public async Task<IActionResult> GetBlueprintPlaceholders([FromQuery] Guid projectId)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (projectId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Project ID is required." });

            try
            {
                var project = await _projectRepository.GetByIdAsync(projectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                var blueprints = await _projectBlueprintRepository.GetListByProjectIdAsync(projectId);
                var result = new List<object>();

                foreach (var bp in blueprints)
                {
                    var cfg = JsonSerializer.Deserialize<Dictionary<string, object>>(bp.BlueprintJson ?? "{}");
                    var printProviderId = 0;
                    var variantIds = new List<int>();

                    if (cfg != null && cfg.TryGetValue("printProviderId", out var ppObj))
                        printProviderId = Convert.ToInt32(ppObj);
                    if (cfg != null && cfg.TryGetValue("variantIds", out var vObj))
                        variantIds = JsonSerializer.Deserialize<List<int>>(vObj.ToString() ?? "[]");

                    var variants = await _variantRepository.GetByBlueprintAndProviderAsync(bp.BlueprintId, printProviderId);
                    var selectedVariants = variants.Where(v => variantIds.Contains(v.VariantId)).ToList();

                    var placeholderList = new List<object>();
                    foreach (var v in selectedVariants)
                    {
                        var phs = await _placeholderRepository.GetByVariantIdAsync(v.VariantId);
                        foreach (var ph in phs)
                        {
                            placeholderList.Add(new
                            {
                                variantId = v.VariantId,
                                variantColor = v.Color,
                                position = ph.Position,
                                decorationMethod = ph.DecorationMethod,
                                height = ph.Height,
                                width = ph.Width
                            });
                        }
                    }

                    result.Add(new
                    {
                        id = bp.Id,
                        blueprintId = bp.BlueprintId,
                        name = bp.Name,
                        placementJson = bp.PlacementJson,
                        placeholders = placeholderList
                    });
                }

                return Json(new ApiResponse { success = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("update-blueprint-placement")]
        public async Task<IActionResult> UpdateBlueprintPlacement([FromBody] UpdateItemBlueprintPlacementRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.Id == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Blueprint ID is required." });

            try
            {
                var blueprint = await _projectBlueprintRepository.GetByIdAsync(request.Id);
                if (blueprint == null)
                    return Json(new ApiResponse { success = false, message = "Blueprint not found." });

                var project = await _projectRepository.GetByIdAsync(blueprint.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                await _projectBlueprintRepository.UpdatePlacementAsync(request.Id, request.PlacementJson ?? "");
                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("update-blueprint-variants")]
        public async Task<IActionResult> UpdateBlueprintVariants([FromBody] UpdateBlueprintVariantsRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.Id == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Blueprint ID is required." });

            try
            {
                var blueprint = await _projectBlueprintRepository.GetByIdAsync(request.Id);
                if (blueprint == null)
                    return Json(new ApiResponse { success = false, message = "Blueprint not found." });

                var project = await _projectRepository.GetByIdAsync(blueprint.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                await _projectBlueprintRepository.UpdateVariantsAsync(request.Id, request.BlueprintJson ?? "", request.PrintProviderId);
                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("update-blueprint-pricing")]
        public async Task<IActionResult> UpdateBlueprintPricing([FromBody] UpdateBlueprintPricingRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.Id == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Blueprint ID is required." });

            try
            {
                var blueprint = await _projectBlueprintRepository.GetByIdAsync(request.Id);
                if (blueprint == null)
                    return Json(new ApiResponse { success = false, message = "Blueprint not found." });

                var project = await _projectRepository.GetByIdAsync(blueprint.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                await _projectBlueprintRepository.UpdatePricingAsync(request.Id, request.PricingJson ?? "[]");
                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("update-blueprint-details")]
        public async Task<IActionResult> UpdateBlueprintDetails([FromBody] UpdateBlueprintDetailsRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.Id == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Blueprint ID is required." });

            if (string.IsNullOrWhiteSpace(request.Name))
                return Json(new ApiResponse { success = false, message = "Name is required." });

            try
            {
                var blueprint = await _projectBlueprintRepository.GetByIdAsync(request.Id);
                if (blueprint == null)
                    return Json(new ApiResponse { success = false, message = "Blueprint not found." });

                var project = await _projectRepository.GetByIdAsync(blueprint.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                await _projectBlueprintRepository.UpdateDetailsAsync(request.Id, request.Name.Trim(), request.Description ?? "", request.Prompt ?? "", request.SafetyInfo ?? "");
                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("generate-blueprint-info")]
        public async Task<IActionResult> GenerateBlueprintInfo([FromBody] GenerateBlueprintInfoRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.Id == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Blueprint ID is required." });

            try
            {
                var blueprint = await _projectBlueprintRepository.GetByIdAsync(request.Id);
                if (blueprint == null)
                    return Json(new ApiResponse { success = false, message = "Blueprint not found." });

                var project = await _projectRepository.GetByIdAsync(blueprint.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                // Get the Printify blueprint for its title/description
                var printifyBlueprint = await _printifyBlueprintRepository.GetByBlueprintIdAsync(blueprint.BlueprintId);
                var bpTitle = printifyBlueprint?.Title ?? "";
                var bpDescription = printifyBlueprint?.Description ?? "";

                // Parse selected variant IDs from blueprintJson
                var selectedColors = new List<string>();
                try
                {
                    var cfg = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(blueprint.BlueprintJson);
                    if (cfg != null && cfg.TryGetValue("variantIds", out var variantEl) && variantEl.ValueKind == JsonValueKind.Array)
                    {
                        var variantIds = variantEl.EnumerateArray().Select(v => v.GetInt32()).ToList();
                        var allVariants = await _variantRepository.GetByBlueprintIdAsync(blueprint.BlueprintId);
                        var variantMap = allVariants.ToDictionary(v => v.VariantId);
                        var colorSet = new HashSet<string>();
                        foreach (var vid in variantIds)
                        {
                            if (variantMap.TryGetValue(vid, out var v) && !string.IsNullOrWhiteSpace(v.Color))
                                colorSet.Add(v.Color);
                        }
                        selectedColors = colorSet.OrderBy(c => c).ToList();
                    }
                }
                catch { /* ignore parse errors */ }

                var colorsDelimited = selectedColors.Count > 0 ? string.Join(", ", selectedColors) : "N/A";

                // Collect artwork prompts for all items assigned to placements (distinct artwork IDs only)
                var artworkPrompts = new List<string>();
                try
                {
                    var placements = JsonSerializer.Deserialize<List<JsonElement>>(blueprint.PlacementJson);
                    if (placements != null)
                    {
                        var itemIds = new HashSet<Guid>();
                        foreach (var p in placements)
                        {
                            if (p.TryGetProperty("source", out var srcEl) && srcEl.GetString() == "item" &&
                                p.TryGetProperty("itemId", out var itemEl) && itemEl.ValueKind != JsonValueKind.Null)
                            {
                                var itemIdStr = itemEl.GetString();
                                if (Guid.TryParse(itemIdStr, out var itemId))
                                    itemIds.Add(itemId);
                            }
                        }

                        var seenArtworkIds = new HashSet<Guid>();
                        foreach (var itemId in itemIds)
                        {
                            var itemArtworks = await _projectItemArtworkRepository.GetByItemIdAsync(itemId);
                            foreach (var a in itemArtworks)
                            {
                                if (string.IsNullOrWhiteSpace(a.Prompt) || !seenArtworkIds.Add(a.Id))
                                    continue;
                                artworkPrompts.Add(a.Prompt);
                            }
                        }
                    }
                }
                catch { /* ignore parse errors */ }

                var artworkPromptsText = artworkPrompts.Count > 0
                    ? string.Join("\n", artworkPrompts.Select((p, i) => $"{i + 1}. {p}"))
                    : "N/A";

                var systemPrompt = "You are a product copywriter for a print-on-demand store. " +
                    "Given context about a product, generate a compelling product title and description. " +
                    "The title should be concise (max 80 characters) and suitable for an e-commerce listing. " +
                    "The description should be 2-4 short paragraphs, written in plain text (no HTML), highlighting the product's appeal. " +
                    "Return ONLY a JSON object with no markdown formatting, in the following structure:\n" +
                    "{\"title\":\"\",\"description\":\"\"}";

                var userPrompt = $"We are generating a title & description for a product.\n\n" +
                    $"Printify Blueprint Name: {bpTitle}\n" +
                    $"Printify Blueprint Description: {bpDescription}\n\n" +
                    $"Project Name: {project.Title}\n" +
                    $"Project Description: {project.Description ?? "N/A"}\n\n" +
                    $"Selected Variant Colors: {colorsDelimited}\n\n" +
                    $"Artwork Prompts:\n{artworkPromptsText}\n\n" +
                    $"Generate a product title and description that would appeal to buyers of this print-on-demand product.";

                string llmOutput;
                try
                {
                    llmOutput = await OpenAI.Prompt(systemPrompt, "", userPrompt, seed: (long)Random.Shared.Next(1, int.MaxValue));
                }
                catch (Exception ex)
                {
                    return Json(new ApiResponse { success = false, message = $"LLM generation failed: {ex.Message}" });
                }

                var rawJson = ExtractFirstJsonObject(llmOutput) ?? llmOutput.Trim();
                string genTitle = "";
                string genDescription = "";
                try
                {
                    using var doc = JsonDocument.Parse(rawJson);
                    if (doc.RootElement.TryGetProperty("title", out var tEl))
                        genTitle = tEl.GetString() ?? "";
                    if (doc.RootElement.TryGetProperty("description", out var dEl))
                        genDescription = dEl.GetString() ?? "";
                }
                catch
                {
                    return Json(new ApiResponse { success = false, message = "Failed to parse LLM response." });
                }

                return Json(new ApiResponse
                {
                    success = true,
                    data = new GenerateBlueprintInfoResponse
                    {
                        Title = genTitle,
                        Description = genDescription
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        private static string? ExtractFirstJsonObject(string input)
        {
            var start = input.IndexOf('{');
            if (start < 0) return null;

            int depth = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = start; i < input.Length; i++)
            {
                var c = input[i];

                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (c == '"')
                    {
                        inString = false;
                    }
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return input.Substring(start, i - start + 1);
                }
            }

            return null;
        }
    }
}
