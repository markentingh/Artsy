using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Artsy.API.Models;
using Artsy.API.Models.Projects;
using Artsy.API.Models.Collections;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.API.Controllers
{
    [Authorize]
    public partial class ProjectsController
    {
        [HttpGet("get-collections")]
        public async Task<IActionResult> GetCollections([FromQuery] Guid projectId)
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

                var collections = (await _projectCollectionRepository.GetByProjectIdAsync(projectId))
                    .OrderBy(c => c.Created)
                    .ToList();

                var result = new List<object>();
                for (int i = 0; i < collections.Count; i++)
                {
                    var collection = collections[i];
                    var artwork = await _projectCollectionArtworkRepository.GetByCollectionIdAsync(collection.Id);
                    var artworkList = artwork.ToList();
                    result.Add(new
                    {
                        id = collection.Id,
                        projectId = collection.ProjectId,
                        title = collection.Title,
                        created = collection.Created,
                        sequence = collections.Count - i,
                        artwork = artworkList.Select(a => new
                        {
                            id = a.Id,
                            itemId = a.ItemId,
                            width = a.Width,
                            height = a.Height,
                            active = a.Active,
                            accepted = a.Accepted
                        })
                    });
                }

                result.Reverse();

                return Json(new ApiResponse { success = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }
    }
}
