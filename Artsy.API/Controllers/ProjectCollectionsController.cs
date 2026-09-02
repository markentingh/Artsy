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
                    var artworkList = artwork.OrderBy(a => a.Index).ToList();
                    var productImages = await _projectCollectionProductImageRepository.GetByCollectionIdAsync(collection.Id);
                    var productImageList = productImages.Where(p => p.Active).ToList();
                    result.Add(new
                    {
                        id = collection.Id,
                        projectId = collection.ProjectId,
                        title = collection.Title,
                        description = collection.Description,
                        created = collection.Created,
                        sequence = i + 1,
                        artwork = artworkList.Select(a => new
                        {
                            id = a.Id,
                            itemId = a.ItemId,
                            width = a.Width,
                            height = a.Height,
                            active = a.Active,
                            accepted = a.Accepted,
                            imageModel = a.ImageModel,
                            index = a.Index
                        }),
                        productImages = productImageList.Select(p => new
                        {
                            id = p.Id,
                            projectBlueprintId = p.ProjectBlueprintId,
                            productImageId = p.ProductImageId,
                            prompt = p.Prompt,
                            accepted = p.Accepted,
                            active = p.Active,
                            generated = p.Generated,
                            imageUrl = $"/api/projects/collection/{collection.Id}/product-image/{p.Id}?thumb=true"
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

        [HttpPost("update-collection-title")]
        public async Task<IActionResult> UpdateCollectionTitle([FromBody] UpdateCollectionTitleRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.Id == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Collection ID is required." });

            if (string.IsNullOrWhiteSpace(request.Title))
                return Json(new ApiResponse { success = false, message = "Title is required." });

            try
            {
                var collection = await _projectCollectionRepository.GetByIdAsync(request.Id);
                if (collection == null)
                    return Json(new ApiResponse { success = false, message = "Collection not found." });

                var project = await _projectRepository.GetByIdAsync(collection.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                collection.Title = request.Title.Trim();
                await _projectCollectionRepository.UpdateTitleAsync(collection.Id, collection.Title);

                return Json(new ApiResponse { success = true, data = new { id = collection.Id, title = collection.Title } });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }
    }
}
