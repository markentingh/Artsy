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

                var reference = new ProjectItemReference
                {
                    ItemId = itemId,
                    ProjectId = item.ProjectId,
                    FileName = file.FileName,
                    Extension = extension,
                    Created = DateTime.UtcNow
                };
                var created = await _projectItemReferenceRepository.CreateAsync(reference);

                await _imageService.SaveProjectItemReferenceAsync(item.ProjectId, created.Id, extension, imageData);

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

                await _imageService.DeleteProjectItemReferenceAsync(reference.ProjectId, reference.Id, reference.Extension);
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

                var bytes = await _imageService.GetProjectItemReferenceAsync(reference.ProjectId, reference.Id, reference.Extension, thumb);
                if (bytes == null || bytes.Length == 0)
                    return NotFound();

                var contentType = reference.Extension == ".png" ? "image/png" : "image/jpeg";
                return File(bytes, contentType);
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
}
