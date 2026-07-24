using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Artsy.API.Models;
using Artsy.API.Models.Projects;
using Artsy.API.Services;
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
                var allReferenceThumbs = await _projectItemReferenceRepository.GetThumbnailsByProjectIdAsync(projectId);
                var allPreviewThumbs = await _projectItemPreviewRepository.GetThumbnailsByProjectIdAsync(projectId);

                var refThumbsByItem = allReferenceThumbs.GroupBy(r => r.ItemId).ToDictionary(g => g.Key, g => g.ToList());
                var previewThumbsByItem = allPreviewThumbs.GroupBy(p => p.ItemId).ToDictionary(g => g.Key, g => g.ToList());

                var result = new List<ProjectItemListItem>();
                foreach (var i in items)
                {
                    var thumbnails = new List<string>();

                    if (refThumbsByItem.TryGetValue(i.Id, out var refs))
                        foreach (var r in refs)
                            thumbnails.Add($"/api/projects/item/{i.Id}/reference/{r.Id}?thumb=true");

                    if (previewThumbsByItem.TryGetValue(i.Id, out var previews))
                        foreach (var p in previews)
                            thumbnails.Add($"/api/projects/item/{i.Id}/preview/{p.Id}?thumb=true");

                    result.Add(new ProjectItemListItem
                    {
                        Id = i.Id,
                        ProjectId = i.ProjectId,
                        Index = i.Index,
                        Title = i.Title,
                        SocialMedia = i.SocialMedia,
                        ProductCount = i.ProductCount,
                        QuestionCount = i.QuestionCount,
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
                    Prompt = ""
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
        public async Task<IActionResult> EstimateItemTokens([FromQuery] Guid itemId, [FromQuery] int width = 1024, [FromQuery] int height = 1024)
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

                var artworkList = await _projectItemArtworkRepository.GetByItemIdAsync(itemId);
                var artwork = artworkList.FirstOrDefault();
                if (artwork == null || string.IsNullOrWhiteSpace(artwork.ImageModel))
                    return Json(new ApiResponse { success = false, message = "No image model configured for this item." });

                var model = await _imageGenerationModelRepository.GetByModelKeyAsync(artwork.ImageModel);
                if (model == null)
                    return Json(new ApiResponse { success = false, message = "Image model not found." });

                if (artwork.ImageModel != "openai")
                    return Json(new ApiResponse
                    {
                        success = true,
                        data = new
                        {
                            textInputTokens = 0,
                            imageInputTokens = 0,
                            imageOutputTokens = 0,
                            estimatedCostUSD = 0m
                        }
                    });

                var references = await _projectItemReferenceRepository.GetByItemIdAsync(itemId);
                var inputImages = references.Select(r => (1024, 1024)).ToList() as IReadOnlyList<(int width, int height)>;

                var estImageGen = _imageGenerations.FirstOrDefault(g => g.ModelKey.Equals(artwork.ImageModel, StringComparison.OrdinalIgnoreCase));
                if (estImageGen == null)
                    return Json(new ApiResponse { success = false, message = "Image model not supported." });

                var tokenizer = estImageGen.CreateTokenizer(model);
                var result = tokenizer.CalculateTokens(
                    artwork.Prompt ?? "",
                    width > 0 ? width : 1024,
                    height > 0 ? height : 1024,
                    "medium",
                    inputImages
                );

                var conversion = model.TokenConversion > 0 ? (1000 * model.TokenConversion) : 1000;

                return Json(new ApiResponse
                {
                    success = true,
                    data = new
                    {
                        textInputTokens = Math.Max(1, (int)(result.TextInputTokens / conversion)),
                        imageInputTokens = Math.Max(1, (int)(result.ImageInputTokens / conversion)),
                        imageOutputTokens = Math.Max(1, (int)(result.ImageOutputTokens / conversion)),
                        estimatedCostUSD = result.EstimatedCostUSD
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
