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

                var items = await _projectItemRepository.GetByProjectIdAsync(projectId);
                var itemIds = items.Select(i => i.Id).ToList();
                var blueprints = await _projectItemBlueprintRepository.GetByItemIdsAsync(itemIds);

                var result = items.Select(i => new ProjectItemListItem
                {
                    Id = i.Id,
                    ProjectId = i.ProjectId,
                    Index = i.Index,
                    Title = i.Title,
                    ProductCount = blueprints.Count(b => b.ItemId == i.Id)
                });

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
                    ImageModelJson = "",
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
                        ProductCount = 0
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

                var blueprints = await _projectItemBlueprintRepository.GetByItemIdAsync(request.Id);
                foreach (var blueprint in blueprints)
                    await _projectItemBlueprintRepository.DeleteAsync(blueprint.Id);

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

    }
}
