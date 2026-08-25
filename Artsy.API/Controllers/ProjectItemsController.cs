using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Artsy.API.Models;
using Artsy.API.Models.Collections;
using Artsy.API.Models.Projects;
using Artsy.API.Services;
using Artsy.Data.Entities;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.API.Controllers
{
    [Authorize]
    public partial class ProjectsController
    {
        [HttpGet("get-items")]
        public async Task<IActionResult> GetItems([FromQuery] Guid projectId)
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

                var items = await _projectItemRepository.GetListByProjectIdAsync(projectId);
                var allArtwork = await _projectItemArtworkRepository.GetByProjectIdAsync(projectId);
                var artworkByItem = allArtwork.ToDictionary(a => a.ItemId, a => a);
                var allReferenceThumbs = await _projectItemReferenceRepository.GetThumbnailsByProjectIdAsync(projectId);
                var allPreviewThumbs = await _projectItemPreviewRepository.GetThumbnailsByProjectIdAsync(projectId);

                var refThumbsByItem = allReferenceThumbs.GroupBy(r => r.ItemId).ToDictionary(g => g.Key, g => g.ToList());
                var previewThumbsByItem = allPreviewThumbs.GroupBy(p => p.ItemId).ToDictionary(g => g.Key, g => g.ToList());

                var result = new List<ProjectItemListItem>();
                foreach (var i in items)
                {
                    var thumbnails = new List<string>();

                    if (artworkByItem.TryGetValue(i.Id, out var art) && art.ArtworkType == "custom" && art.CustomImageId.HasValue)
                    {
                        thumbnails.Add($"/api/custom-images/custom-image/{art.CustomImageId.Value}?thumb=true");
                    }
                    else
                    {
                        if (previewThumbsByItem.TryGetValue(i.Id, out var previews))
                            foreach (var p in previews)
                                thumbnails.Add($"/api/projects/item/{i.Id}/preview/{p.Id}?thumb=true");

                        if (refThumbsByItem.TryGetValue(i.Id, out var refs))
                            foreach (var r in refs)
                                thumbnails.Add($"/api/projects/item/{i.Id}/reference/{r.Id}?thumb=true");
                    }

                    if (artworkByItem.TryGetValue(i.Id, out var artwork))
                    {
                        // no-op - keeps the compiler from warning about assigned variable
                    }

                    result.Add(new ProjectItemListItem
                    {
                        Id = i.Id,
                        ProjectId = i.ProjectId,
                        Index = i.Index,
                        Title = i.Title,
                        SocialMedia = i.SocialMedia,
                        ProductCount = i.ProductCount,
                        QuestionCount = i.QuestionCount,
                        ArtworkType = artwork?.ArtworkType ?? "ai",
                        OpacityJson = artwork?.OpacityJson,
                        Thumbnails = thumbnails
                    });
                }

                return Json(new ApiResponse { success = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("create-item")]
        public async Task<IActionResult> CreateItem([FromBody] CreateProjectItemRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.ProjectId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Project ID is required." });

            try
            {
                var project = await _projectRepository.GetByIdAsync(request.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                var existingItems = await _projectItemRepository.GetByProjectIdAsync(request.ProjectId);
                var nextIndex = existingItems.Any() ? existingItems.Max(i => i.Index) + 1 : 1;

                var item = new ProjectItem
                {
                    ProjectId = request.ProjectId,
                    Index = nextIndex,
                    Title = request.Title
                };
                var created = await _projectItemRepository.CreateAsync(item);

                var artwork = new ProjectItemArtwork
                {
                    ItemId = created.Id,
                    ProjectId = created.ProjectId,
                    ImageModel = "openai",
                    Prompt = "",
                    AspectRatio = string.IsNullOrWhiteSpace(request.AspectRatio) ? "1:1" : request.AspectRatio
                };
                await _projectItemArtworkRepository.CreateAsync(artwork);

                return Json(new ApiResponse
                {
                    success = true,
                    data = new ProjectItemListItem
                    {
                        Id = created.Id,
                        ProjectId = created.ProjectId,
                        Index = created.Index,
                        Title = created.Title,
                        ProductCount = 0,
                        QuestionCount = 0,
                        ArtworkType = "ai",
                        Thumbnails = new List<string>()
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("delete-item")]
        public async Task<IActionResult> DeleteItem([FromBody] DeleteProjectItemRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.Id == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Item ID is required." });

            try
            {
                var item = await _projectItemRepository.GetByIdAsync(request.Id);
                if (item == null)
                    return Json(new ApiResponse { success = false, message = "Item not found." });

                var project = await _projectRepository.GetByIdAsync(item.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                var questions = await _projectItemQuestionRepository.GetByItemIdAsync(request.Id);
                foreach (var question in questions)
                    await _projectItemQuestionRepository.DeleteAsync(question.Id);

                await _projectItemRepository.DeleteAsync(request.Id);
                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("update-item-title")]
        public async Task<IActionResult> UpdateItemTitle([FromBody] UpdateProjectItemTitleRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.Id == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Item ID is required." });

            try
            {
                var item = await _projectItemRepository.GetByIdAsync(request.Id);
                if (item == null)
                    return Json(new ApiResponse { success = false, message = "Item not found." });

                var project = await _projectRepository.GetByIdAsync(item.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                item.Title = request.Title;
                await _projectItemRepository.UpdateAsync(item);
                return Json(new ApiResponse { success = true, data = item });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("update-item-social-media")]
        public async Task<IActionResult> UpdateItemSocialMedia([FromBody] UpdateProjectItemSocialMediaRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.Id == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Item ID is required." });

            try
            {
                var item = await _projectItemRepository.GetByIdAsync(request.Id);
                if (item == null)
                    return Json(new ApiResponse { success = false, message = "Item not found." });

                var project = await _projectRepository.GetByIdAsync(item.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                item.SocialMedia = request.SocialMedia;
                await _projectItemRepository.UpdateAsync(item);
                return Json(new ApiResponse { success = true, data = item });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("reorder-items")]
        public async Task<IActionResult> ReorderItems([FromBody] ReorderProjectItemsRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.ProjectId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Project ID is required." });

            if (request.ItemIds == null || request.ItemIds.Count == 0)
                return Json(new ApiResponse { success = false, message = "Item IDs are required." });

            try
            {
                var project = await _projectRepository.GetByIdAsync(request.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                await _projectItemRepository.ReorderAsync(request.ItemIds);
                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("estimate-item-tokens")]
        public async Task<IActionResult> EstimateItemTokens([FromQuery] Guid itemId, [FromQuery] int modelId = 0, [FromQuery] Guid? collectionId = null)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (itemId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Item ID is required." });

            try
            {
                var item = await _projectItemRepository.GetByIdAsync(itemId);
                if (item == null)
                    return Json(new ApiResponse { success = false, message = "Item not found." });

                var project = await _projectRepository.GetByIdAsync(item.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                // Build the generation plan with collectionId so it filters by active products
                // Use resolutionTier 2 (2K) to match the actual collection wizard generation
                var plan = await _artworkGenerationPlanService.BuildPlanAsync(item.ProjectId, collectionId ?? Guid.Empty, itemId, resolutionTier: 2);

                ImageGenerationModel? model;
                if (modelId > 0)
                    model = await _imageGenerationModelRepository.GetByIdAsync(modelId);
                else
                    model = await _imageGenerationModelRepository.GetByModelKeyAsync(plan.Artwork.ImageModel);
                if (model == null)
                    return Json(new ApiResponse { success = false, message = "Image model not found." });

                var estImageGen = _imageGenerations.FirstOrDefault(g => g.ModelKey.Equals(model.ModelKey, StringComparison.OrdinalIgnoreCase));
                if (estImageGen == null)
                    return Json(new ApiResponse { success = false, message = "Image model not supported." });

                var tokenizer = estImageGen.CreateTokenizer(model);
                var tokenCost = _tokenCostOptions.Cost > 0 ? _tokenCostOptions.Cost : 0.01m;

                // Use reference image dimensions from the plan
                var inputImageDimensions = plan.ReferenceImages
                    .Where(r => r.Width > 0 && r.Height > 0)
                    .Select(r => (r.Width, r.Height))
                    .ToList() as IReadOnlyList<(int width, int height)>;

                // Build detailed response with per-task tokens and placements
                var generations = new List<CollectionArtworkGenerationDto>();
                var totalTokens = 0m;
                foreach (var task in plan.Tasks)
                {
                    var result = tokenizer.CalculateTokens(
                        plan.FinalPrompt,
                        task.Width,
                        task.Height,
                        "high",
                        inputImageDimensions,
                        "auto",
                        tokenCost
                    );

                    generations.Add(new CollectionArtworkGenerationDto
                    {
                        ItemId = itemId,
                        Width = task.Width,
                        Height = task.Height,
                        NeedsUpscale = plan.NeedsUpscale,
                        Tokens = result.PlatformTokens,
                        Placements = task.Placements.Select(p => new EstimatePlacementDto
                        {
                            BlueprintId = p.BlueprintId,
                            BlueprintName = p.BlueprintName,
                            Position = p.Position,
                            Width = p.Width,
                            Height = p.Height
                        }).ToList()
                    });

                    totalTokens += result.PlatformTokens;
                }

                return Json(new ApiResponse
                {
                    success = true,
                    data = new
                    {
                        totalTokens = (int)Math.Ceiling(totalTokens),
                        generations
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

    }
}
