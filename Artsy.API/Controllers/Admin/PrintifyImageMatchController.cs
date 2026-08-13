using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Artsy.API.Models;
using Artsy.Auth.Policies;
using Artsy.Data.Entities;
using Artsy.Data.Interfaces;

namespace Artsy.API.Controllers.Admin
{
    [Route("/api/admin/printify-image-match")]
    [Authorize(Policy = nameof(AuthConstants.Policy.ManageUsers))]
    public class PrintifyImageMatchController : ApiController
    {
        readonly IPrintifyBlueprintRepository _printifyBlueprintRepo;
        readonly IPrintifyBlueprintImageRepository _imageRepo;
        readonly IPrintifyBlueprintImageVariantRepository _imageVariantRepo;

        public PrintifyImageMatchController(
            IPrintifyBlueprintRepository printifyBlueprintRepo,
            IPrintifyBlueprintImageRepository imageRepo,
            IPrintifyBlueprintImageVariantRepository imageVariantRepo)
        {
            _printifyBlueprintRepo = printifyBlueprintRepo;
            _imageRepo = imageRepo;
            _imageVariantRepo = imageVariantRepo;
        }

        [HttpGet("unpublished-blueprints")]
        public async Task<IActionResult> GetUnpublishedBlueprints()
        {
            try
            {
                var results = await _printifyBlueprintRepo.SearchAsync("", "all", 0, 10000, false, "oldest");
                return Json(new ApiResponse
                {
                    success = true,
                    data = results.Select(bp => new
                    {
                        id = bp.BlueprintId,
                        title = bp.Title,
                        brand = bp.Brand,
                        model = bp.Model,
                        description = bp.Description,
                        imageCount = bp.ImageCount,
                        published = bp.Published
                    })
                });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("blueprints/{blueprintId}")]
        public async Task<IActionResult> GetBlueprint(int blueprintId)
        {
            try
            {
                var bp = await _printifyBlueprintRepo.GetByBlueprintIdAsync(blueprintId);
                if (bp == null)
                    return Json(new ApiResponse { success = false, message = "Blueprint not found" });

                return Json(new ApiResponse
                {
                    success = true,
                    data = new
                    {
                        id = bp.BlueprintId,
                        title = bp.Title,
                        brand = bp.Brand,
                        model = bp.Model,
                        description = bp.Description,
                        imageCount = bp.ImageCount,
                        published = bp.Published
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("blueprints/{blueprintId}/images")]
        public async Task<IActionResult> GetBlueprintImages(int blueprintId)
        {
            try
            {
                var images = (await _imageRepo.GetByBlueprintIdAsync(blueprintId)).ToList();
                var imageIds = images.Select(img => img.Id).ToList();
                var variants = (await _imageVariantRepo.GetByBlueprintImageIdsAsync(imageIds)).ToList();
                var variantsByImageId = variants.GroupBy(v => v.BlueprintImageId)
                    .ToDictionary(g => g.Key, g => g.Select(v => v.VariantColor).ToList());

                return Json(new ApiResponse
                {
                    success = true,
                    data = images.Select(img => new
                    {
                        id = img.Id,
                        blueprintId = img.BlueprintId,
                        imageIndex = img.ImageIndex,
                        variantColors = variantsByImageId.TryGetValue(img.Id, out var colors) ? colors : new List<string>(),
                        type = img.Type,
                        position = img.Position
                    })
                });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("blueprints/{blueprintId}/images/{imageIndex}/apply-variants")]
        public async Task<IActionResult> ApplyVariants(int blueprintId, int imageIndex, [FromBody] JsonElement body)
        {
            try
            {
                var images = (await _imageRepo.GetByBlueprintIdAsync(blueprintId)).ToList();
                var img = images.FirstOrDefault(i => i.ImageIndex == imageIndex);

                var selectedColors = new List<string>();
                if (body.TryGetProperty("selectedColors", out var colorsArr) && colorsArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var c in colorsArr.EnumerateArray())
                    {
                        var s = c.GetString();
                        if (!string.IsNullOrWhiteSpace(s))
                            selectedColors.Add(s);
                    }
                }

                var type = body.TryGetProperty("type", out var t) ? t.GetInt32() : 0;
                var position = body.TryGetProperty("position", out var p) ? p.GetInt32() : 0;

                Guid imageId;
                if (img == null)
                {
                    imageId = await _imageRepo.UpsertAsync(new PrintifyBlueprintImage
                    {
                        BlueprintId = blueprintId,
                        ImageIndex = imageIndex,
                        Type = type,
                        Position = position
                    });
                }
                else
                {
                    imageId = img.Id;
                    if (img.Type != type || img.Position != position)
                    {
                        imageId = await _imageRepo.UpsertAsync(new PrintifyBlueprintImage
                        {
                            Id = img.Id,
                            BlueprintId = blueprintId,
                            ImageIndex = imageIndex,
                            Type = type,
                            Position = position
                        });
                    }
                }

                await _imageVariantRepo.DeleteByBlueprintImageIdAsync(imageId);
                if (selectedColors.Count > 0)
                    await _imageVariantRepo.UpsertAsync(imageId, selectedColors);

                return Json(new ApiResponse
                {
                    success = true,
                    data = new
                    {
                        imageId,
                        imageIndex,
                        appliedCount = selectedColors.Count
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("blueprints/{blueprintId}/publish")]
        public async Task<IActionResult> PublishBlueprint(int blueprintId)
        {
            try
            {
                await _printifyBlueprintRepo.UpdatePublishedAsync(blueprintId, true);
                return Json(new ApiResponse { success = true, data = new { blueprintId, published = true } });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }
    }
}
