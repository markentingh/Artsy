using Microsoft.AspNetCore.Mvc;
using Artsy.API.Models;
using Artsy.API.Models.Projects;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.API.Controllers
{
    public partial class ProjectsController
    {
        [HttpGet("get-placement-groups")]
        public async Task<IActionResult> GetPlacementGroups([FromQuery] Guid projectId, [FromQuery] int blueprintId)
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

                var groups = await _placementGroupRepository.GetByProjectAndBlueprintAsync(projectId, blueprintId);
                var groupIds = groups.Select(g => g.Id).ToList();
                var images = groupIds.Count > 0
                    ? await _placementGroupImageRepository.GetByProjectAndBlueprintAsync(projectId, blueprintId)
                    : Enumerable.Empty<ProjectBlueprintPlacementGroupImage>();

                var result = groups.Select(g => new
                {
                    id = g.Id,
                    projectId = g.ProjectId,
                    blueprintId = g.BlueprintId,
                    images = images.Where(i => i.GroupId == g.Id).OrderBy(i => i.Index).Select(i => new
                    {
                        id = i.Id,
                        groupId = i.GroupId,
                        index = i.Index,
                        artworkId = i.ArtworkId,
                        customId = i.CustomId,
                        position = i.Position,
                        flipX = i.FlipX,
                        flipY = i.FlipY,
                    }),
                });

                return Json(new ApiResponse { success = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("create-placement-group")]
        public async Task<IActionResult> CreatePlacementGroup([FromBody] CreatePlacementGroupRequest request)
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

                var group = new ProjectBlueprintPlacementGroup
                {
                    ProjectId = request.ProjectId,
                    BlueprintId = request.BlueprintId,
                };
                var created = await _placementGroupRepository.CreateAsync(group);
                return Json(new ApiResponse { success = true, data = new { id = created.Id, projectId = created.ProjectId, blueprintId = created.BlueprintId, images = Enumerable.Empty<object>() } });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("delete-placement-group")]
        public async Task<IActionResult> DeletePlacementGroup([FromBody] DeletePlacementGroupRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            try
            {
                await _placementGroupImageRepository.DeleteByGroupIdAsync(request.GroupId);
                await _placementGroupRepository.DeleteAsync(request.GroupId);
                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("save-placement-group-image")]
        public async Task<IActionResult> SavePlacementGroupImage([FromBody] SavePlacementGroupImageRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            try
            {
                if (request.Id.HasValue)
                {
                    var existing = new ProjectBlueprintPlacementGroupImage
                    {
                        Id = request.Id.Value,
                        GroupId = request.GroupId,
                        Index = request.Index,
                        ArtworkId = request.ArtworkId,
                        CustomId = request.CustomId,
                        Position = request.Position,
                        FlipX = request.FlipX,
                        FlipY = request.FlipY,
                        ProjectId = request.ProjectId,
                        BlueprintId = request.BlueprintId,
                    };
                    await _placementGroupImageRepository.UpdateAsync(existing);
                    return Json(new ApiResponse { success = true, data = new { id = existing.Id } });
                }
                else
                {
                    var image = new ProjectBlueprintPlacementGroupImage
                    {
                        ProjectId = request.ProjectId,
                        BlueprintId = request.BlueprintId,
                        GroupId = request.GroupId,
                        Index = request.Index,
                        ArtworkId = request.ArtworkId,
                        CustomId = request.CustomId,
                        Position = request.Position,
                        FlipX = request.FlipX,
                        FlipY = request.FlipY,
                    };
                    var created = await _placementGroupImageRepository.CreateAsync(image);
                    return Json(new ApiResponse { success = true, data = new { id = created.Id } });
                }
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("delete-placement-group-image")]
        public async Task<IActionResult> DeletePlacementGroupImage([FromBody] DeletePlacementGroupImageRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            try
            {
                await _placementGroupImageRepository.DeleteAsync(request.Id);
                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }
    }
}
