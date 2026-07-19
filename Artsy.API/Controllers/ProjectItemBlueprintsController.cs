using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Artsy.API.Models;
using Artsy.API.Models.Projects;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.API.Controllers
{
    [Authorize]
    public partial class ProjectsController
    {
        [HttpGet("get-item-blueprints")]
        public async Task<IActionResult> GetItemBlueprints([FromQuery] Guid itemId)
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

                var blueprints = await _projectItemBlueprintRepository.GetByItemIdAsync(itemId);
                return Json(new ApiResponse { success = true, data = blueprints });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("create-item-blueprint")]
        public async Task<IActionResult> CreateItemBlueprint([FromBody] CreateProjectItemBlueprintRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.ItemId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Item ID is required." });

            if (string.IsNullOrWhiteSpace(request.Name))
                return Json(new ApiResponse { success = false, message = "Name is required." });

            try
            {
                var item = await _projectItemRepository.GetByIdAsync(request.ItemId);
                if (item == null)
                    return Json(new ApiResponse { success = false, message = "Item not found." });

                var project = await _projectRepository.GetByIdAsync(item.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                var blueprint = new ProjectItemBlueprint
                {
                    ItemId = request.ItemId,
                    ProjectId = item.ProjectId,
                    BlueprintId = request.BlueprintId,
                    Name = request.Name.Trim(),
                    BlueprintJson = request.BlueprintJson ?? ""
                };
                var created = await _projectItemBlueprintRepository.CreateAsync(blueprint);
                return Json(new ApiResponse { success = true, data = created });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("delete-item-blueprint")]
        public async Task<IActionResult> DeleteItemBlueprint([FromBody] DeleteProjectItemBlueprintRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.Id == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Blueprint ID is required." });

            try
            {
                var blueprint = await _projectItemBlueprintRepository.GetByIdAsync(request.Id);
                if (blueprint == null)
                    return Json(new ApiResponse { success = false, message = "Blueprint not found." });

                var project = await _projectRepository.GetByIdAsync(blueprint.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                await _projectItemBlueprintRepository.DeleteAsync(request.Id);
                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("update-item-blueprint")]
        public async Task<IActionResult> UpdateItemBlueprint([FromBody] UpdateProjectItemBlueprintRequest request)
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
                var blueprint = await _projectItemBlueprintRepository.GetByIdAsync(request.Id);
                if (blueprint == null)
                    return Json(new ApiResponse { success = false, message = "Blueprint not found." });

                var project = await _projectRepository.GetByIdAsync(blueprint.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                blueprint.BlueprintId = request.BlueprintId;
                blueprint.Name = request.Name.Trim();
                blueprint.BlueprintJson = request.BlueprintJson ?? "";
                await _projectItemBlueprintRepository.UpdateAsync(blueprint);
                return Json(new ApiResponse { success = true, data = blueprint });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }
    }
}
