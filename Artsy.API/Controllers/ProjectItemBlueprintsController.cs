using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Artsy.API.Models;
using Artsy.API.Models.Projects;
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

                var placements = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(placementJson);
                if (placements == null || placements.Count == 0) return false;
                var hasPlacements = placements.Any(p =>
                {
                    if (!p.Value.TryGetProperty("source", out var srcEl)) return false;
                    var source = srcEl.GetString() ?? "";
                    if (string.IsNullOrWhiteSpace(source)) return false;
                    if (source == "item" && p.Value.TryGetProperty("itemId", out var itemEl) && itemEl.ValueKind != JsonValueKind.Null)
                        return true;
                    if (source == "custom" && p.Value.TryGetProperty("customImageId", out var imgEl) && imgEl.ValueKind != JsonValueKind.Null)
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
                    ImageCount = b.ImageCount
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
    }
}
