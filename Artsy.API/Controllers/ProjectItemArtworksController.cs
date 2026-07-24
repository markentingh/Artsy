using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Artsy.API.Models;
using Artsy.API.Models.Projects;
using Artsy.Data.Entities.Projects;

namespace Artsy.API.Controllers
{
    [Authorize]
    public partial class ProjectsController
    {
        [HttpGet("get-item-artwork")]
        public async Task<IActionResult> GetItemArtwork([FromQuery] Guid itemId)
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

                return Json(new ApiResponse { success = true, data = artwork });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("update-item-prompt")]
        public async Task<IActionResult> UpdateItemPrompt([FromBody] UpdateProjectItemPromptRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.ItemId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Item ID is required." });

            try
            {
                var item = await _projectItemRepository.GetByIdAsync(request.ItemId);
                if (item == null)
                    return Json(new ApiResponse { success = false, message = "Item not found." });

                var project = await _projectRepository.GetByIdAsync(item.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                var artworkList = await _projectItemArtworkRepository.GetByItemIdAsync(request.ItemId);
                var artwork = artworkList.FirstOrDefault();
                if (artwork == null)
                {
                    artwork = new ProjectItemArtwork
                    {
                        ItemId = request.ItemId,
                        ProjectId = item.ProjectId,
                        Prompt = request.Prompt ?? ""
                    };
                    var created = await _projectItemArtworkRepository.CreateAsync(artwork);
                    return Json(new ApiResponse { success = true, data = created });
                }
                else
                {
                    artwork.Prompt = request.Prompt ?? "";
                    await _projectItemArtworkRepository.UpdateAsync(artwork);
                    return Json(new ApiResponse { success = true, data = artwork });
                }
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("update-item-image-model")]
        public async Task<IActionResult> UpdateItemImageModel([FromBody] UpdateProjectItemImageModelRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.ItemId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Item ID is required." });

            if (string.IsNullOrWhiteSpace(request.ImageModel))
                return Json(new ApiResponse { success = false, message = "Image model is required." });

            try
            {
                var item = await _projectItemRepository.GetByIdAsync(request.ItemId);
                if (item == null)
                    return Json(new ApiResponse { success = false, message = "Item not found." });

                var project = await _projectRepository.GetByIdAsync(item.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                var artworkList = await _projectItemArtworkRepository.GetByItemIdAsync(request.ItemId);
                var artwork = artworkList.FirstOrDefault();
                if (artwork == null)
                {
                    artwork = new ProjectItemArtwork
                    {
                        ItemId = request.ItemId,
                        ProjectId = item.ProjectId,
                        ImageModel = request.ImageModel,
                        Prompt = ""
                    };
                    var created = await _projectItemArtworkRepository.CreateAsync(artwork);
                    return Json(new ApiResponse { success = true, data = created });
                }
                else
                {
                    artwork.ImageModel = request.ImageModel;
                    await _projectItemArtworkRepository.UpdateAsync(artwork);
                    return Json(new ApiResponse { success = true, data = artwork });
                }
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }
        [HttpPost("update-item-artwork-type")]
        public async Task<IActionResult> UpdateItemArtworkType([FromBody] UpdateProjectItemArtworkTypeRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.ItemId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Item ID is required." });

            try
            {
                var item = await _projectItemRepository.GetByIdAsync(request.ItemId);
                if (item == null)
                    return Json(new ApiResponse { success = false, message = "Item not found." });

                var project = await _projectRepository.GetByIdAsync(item.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                var artworkList = await _projectItemArtworkRepository.GetByItemIdAsync(request.ItemId);
                var artwork = artworkList.FirstOrDefault();
                if (artwork == null)
                {
                    artwork = new ProjectItemArtwork
                    {
                        ItemId = request.ItemId,
                        ProjectId = item.ProjectId,
                        ArtworkType = request.ArtworkType,
                        CustomImageId = request.CustomImageId
                    };
                    var created = await _projectItemArtworkRepository.CreateAsync(artwork);
                    return Json(new ApiResponse { success = true, data = created });
                }
                else
                {
                    artwork.ArtworkType = request.ArtworkType;
                    artwork.CustomImageId = request.CustomImageId;
                    await _projectItemArtworkRepository.UpdateAsync(artwork);
                    return Json(new ApiResponse { success = true, data = artwork });
                }
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }
    }
}
