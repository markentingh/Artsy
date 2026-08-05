using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Artsy.API.Models;
using Artsy.Data.Entities.Projects;

namespace Artsy.API.Controllers
{
    [Authorize]
    public partial class ProjectsController
    {
        [HttpGet("get-item-references")]
        public async Task<IActionResult> GetItemReferences([FromQuery] Guid itemId)
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

                var references = await _projectItemReferenceRepository.GetByItemIdAsync(itemId);
                return Json(new ApiResponse { success = true, data = references });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("get-all-item-references")]
        public async Task<IActionResult> GetProjectReferences([FromQuery] Guid projectId)
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

                var references = await _projectItemReferenceRepository.GetByProjectIdAsync(projectId);
                return Json(new ApiResponse { success = true, data = references });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("upload-item-reference")]
        public async Task<IActionResult> UploadItemReference([FromForm] IFormFile file, [FromForm] Guid itemId)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (itemId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Item ID is required." });

            if (file == null || file.Length == 0)
                return Json(new ApiResponse { success = false, message = "File is required." });

            var allowedTypes = new[] { "image/jpeg", "image/png" };
            if (!allowedTypes.Contains(file.ContentType))
                return Json(new ApiResponse { success = false, message = "Only JPG and PNG files are allowed." });

            try
            {
                var item = await _projectItemRepository.GetByIdAsync(itemId);
                if (item == null)
                    return Json(new ApiResponse { success = false, message = "Item not found." });

                var project = await _projectRepository.GetByIdAsync(item.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (extension != ".jpg" && extension != ".jpeg" && extension != ".png")
                    return Json(new ApiResponse { success = false, message = "Only JPG and PNG files are allowed." });

                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                var imageData = ms.ToArray();

                var customImage = new CustomImage
                {
                    AppUserId = userId,
                    FileName = file.FileName,
                    Extension = extension
                };
                var createdImage = await _customImageRepository.CreateAsync(customImage);
                await _imageService.SaveCustomImageAsync(userId, createdImage.Id, extension, imageData);

                var reference = new ProjectItemReference
                {
                    ItemId = itemId,
                    ProjectId = item.ProjectId,
                    CustomImageId = createdImage.Id,
                    Created = DateTime.UtcNow
                };
                var created = await _projectItemReferenceRepository.CreateAsync(reference);

                return Json(new ApiResponse { success = true, data = created });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("delete-item-reference")]
        public async Task<IActionResult> DeleteItemReference([FromBody] DeleteItemReferenceRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.Id == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Reference ID is required." });

            try
            {
                var reference = await _projectItemReferenceRepository.GetByIdAsync(request.Id);
                if (reference == null)
                    return Json(new ApiResponse { success = false, message = "Reference not found." });

                var project = await _projectRepository.GetByIdAsync(reference.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                await _projectItemReferenceRepository.DeleteAsync(request.Id);

                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("item/{itemId}/reference/{referenceId}")]
        public async Task<IActionResult> GetItemReference(Guid itemId, Guid referenceId, [FromQuery] bool thumb = false)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (itemId == Guid.Empty || referenceId == Guid.Empty)
                return NotFound();

            try
            {
                var reference = await _projectItemReferenceRepository.GetByIdAsync(referenceId);
                if (reference == null || reference.ItemId != itemId)
                    return NotFound();

                var project = await _projectRepository.GetByIdAsync(reference.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                byte[]? bytes = null;

                if (reference.ArtworkId.HasValue)
                {
                    var refPreviews = await _projectItemPreviewRepository.GetByItemIdAsync(reference.ArtworkId.Value);
                    var newestPreview = refPreviews.FirstOrDefault();
                    if (newestPreview != null)
                    {
                        bytes = await _imageService.GetProjectItemPreviewAsync(reference.ProjectId, reference.ArtworkId.Value, newestPreview.Id, thumb);
                    }
                }
                else if (reference.CustomImageId.HasValue)
                {
                    var customImage = await _customImageRepository.GetByIdAsync(reference.CustomImageId.Value);
                    if (customImage == null)
                        return NotFound();
                    bytes = await _imageService.GetCustomImageAsync(customImage.AppUserId, customImage.Id, customImage.Extension, thumb);
                }

                if (bytes == null || bytes.Length == 0)
                    return NotFound();

                return File(bytes, "image/jpeg");
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("add-artwork-reference")]
        public async Task<IActionResult> AddArtworkReference([FromBody] AddArtworkReferenceRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.ItemId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Item ID is required." });

            if (request.ArtworkId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Artwork ID is required." });

            try
            {
                var item = await _projectItemRepository.GetByIdAsync(request.ItemId);
                if (item == null)
                    return Json(new ApiResponse { success = false, message = "Item not found." });

                var project = await _projectRepository.GetByIdAsync(item.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                var artworkItem = await _projectItemRepository.GetByIdAsync(request.ArtworkId);
                if (artworkItem == null || artworkItem.ProjectId != item.ProjectId)
                    return Json(new ApiResponse { success = false, message = "Artwork not found in this project." });

                if (artworkItem.Index >= item.Index)
                    return Json(new ApiResponse { success = false, message = "Can only reference artworks with a lower index." });

                var reference = new ProjectItemReference
                {
                    ItemId = request.ItemId,
                    ProjectId = item.ProjectId,
                    ArtworkId = request.ArtworkId,
                    Created = DateTime.UtcNow
                };
                var created = await _projectItemReferenceRepository.CreateAsync(reference);

                return Json(new ApiResponse { success = true, data = created });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("add-custom-image-reference")]
        public async Task<IActionResult> AddCustomImageReference([FromBody] AddCustomImageReferenceRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.ItemId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Item ID is required." });

            if (request.CustomImageId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Custom image ID is required." });

            try
            {
                var item = await _projectItemRepository.GetByIdAsync(request.ItemId);
                if (item == null)
                    return Json(new ApiResponse { success = false, message = "Item not found." });

                var project = await _projectRepository.GetByIdAsync(item.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                var customImage = await _customImageRepository.GetByIdAsync(request.CustomImageId);
                if (customImage == null || customImage.AppUserId != userId)
                    return Json(new ApiResponse { success = false, message = "Custom image not found." });

                var reference = new ProjectItemReference
                {
                    ItemId = request.ItemId,
                    ProjectId = item.ProjectId,
                    CustomImageId = request.CustomImageId,
                    Created = DateTime.UtcNow
                };
                var created = await _projectItemReferenceRepository.CreateAsync(reference);

                return Json(new ApiResponse { success = true, data = created });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }
    }

    public class DeleteItemReferenceRequest
    {
        public Guid Id { get; set; }
    }

    public class AddArtworkReferenceRequest
    {
        public Guid ItemId { get; set; }
        public Guid ArtworkId { get; set; }
    }

    public class AddCustomImageReferenceRequest
    {
        public Guid ItemId { get; set; }
        public Guid CustomImageId { get; set; }
    }
}
