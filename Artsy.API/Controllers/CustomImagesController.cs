using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Artsy.API.Models;
using Artsy.API.Services;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.API.Controllers
{
    [Route("/api/custom-images")]
    [Authorize]
    public class CustomImagesController : ApiController
    {
        readonly ICustomImageRepository _customImageRepository;
        readonly IImageService _imageService;

        public CustomImagesController(ICustomImageRepository customImageRepository, IImageService imageService)
        {
            _customImageRepository = customImageRepository;
            _imageService = imageService;
        }

        [HttpGet("get-custom-images")]
        public async Task<IActionResult> GetCustomImages([FromQuery] int limit = 10, [FromQuery] int offset = 0)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            try
            {
                var images = await _customImageRepository.GetByUserIdAsync(userId, limit, offset);
                var totalCount = await _customImageRepository.CountByUserIdAsync(userId);
                return Json(new ApiResponse { success = true, data = new { images, totalCount } });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("upload-custom-image")]
        public async Task<IActionResult> UploadCustomImage([FromForm] IFormFile file)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (file == null || file.Length == 0)
                return Json(new ApiResponse { success = false, message = "File is required." });

            var allowedTypes = new[] { "image/jpeg", "image/png" };
            if (!allowedTypes.Contains(file.ContentType))
                return Json(new ApiResponse { success = false, message = "Only JPG and PNG files are allowed." });

            try
            {
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
                var created = await _customImageRepository.CreateAsync(customImage);

                await _imageService.SaveCustomImageAsync(userId, created.Id, extension, imageData);

                return Json(new ApiResponse { success = true, data = created });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("delete-custom-image")]
        public async Task<IActionResult> DeleteCustomImage([FromBody] DeleteCustomImageRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.Id == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Image ID is required." });

            try
            {
                var image = await _customImageRepository.GetByIdAsync(request.Id);
                if (image == null || image.AppUserId != userId)
                    return Json(new ApiResponse { success = false, message = "Image not found." });

                await _imageService.DeleteCustomImageAsync(userId, image.Id, image.Extension);
                await _customImageRepository.DeleteAsync(request.Id);

                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("custom-image/{imageId}")]
        public async Task<IActionResult> GetCustomImageFile(Guid imageId, [FromQuery] bool thumb = false)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (imageId == Guid.Empty)
                return NotFound();

            try
            {
                var image = await _customImageRepository.GetByIdAsync(imageId);
                if (image == null || image.AppUserId != userId)
                    return NotFound();

                var bytes = await _imageService.GetCustomImageAsync(userId, imageId, image.Extension, thumb);
                if (bytes == null || bytes.Length == 0)
                    return NotFound();

                var contentType = image.Extension == ".png" ? "image/png" : "image/jpeg";
                return File(bytes, contentType);
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }
    }

    public class DeleteCustomImageRequest
    {
        public Guid Id { get; set; }
    }
}
