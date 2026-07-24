using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Artsy.API.Models;
using Artsy.API.Services;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.API.Controllers
{
    [Authorize]
    public partial class ProjectsController
    {
        [HttpGet("image-generation/{id}")]
        public async Task<IActionResult> GetImageGeneration(Guid id)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (id == Guid.Empty)
                return NotFound();

            try
            {
                var generation = await _projectImageGenerationRepository.GetByIdAsync(id);
                if (generation == null)
                    return NotFound();

                var project = await _projectRepository.GetByIdAsync(generation.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                if (string.IsNullOrWhiteSpace(generation.Filename))
                    return NotFound();

                var bytes = await _imageService.GetImageGenerationAsync(
                    generation.ProjectId,
                    generation.ItemId,
                    generation.CollectionId,
                    generation.BlueprintId,
                    generation.Filename);

                if (bytes == null || bytes.Length == 0)
                    return NotFound();

                return File(bytes, "image/jpeg");
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }
    }
}
