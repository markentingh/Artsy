using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Artsy.API.Models;
using Artsy.API.Models.Projects;
using Artsy.API.Services;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.API.Controllers
{
    [Route("/api/projects")]
    [Authorize]
    public class ProjectsController : ApiController
    {
        readonly IProjectRepository _projectRepository;
        readonly IProjectCollectionRepository _projectCollectionRepository;
        readonly IProjectItemRepository _projectItemRepository;
        readonly IProjectItemBlueprintRepository _projectItemBlueprintRepository;
        readonly IProjectQuestionRepository _projectQuestionRepository;
        readonly IProjectCollectionArtworkRepository _projectCollectionArtworkRepository;
        readonly IImageService _imageService;

        public ProjectsController(
            IProjectRepository projectRepository,
            IProjectCollectionRepository projectCollectionRepository,
            IProjectItemRepository projectItemRepository,
            IProjectItemBlueprintRepository projectItemBlueprintRepository,
            IProjectQuestionRepository projectQuestionRepository,
            IProjectCollectionArtworkRepository projectCollectionArtworkRepository,
            IImageService imageService)
        {
            _projectRepository = projectRepository;
            _projectCollectionRepository = projectCollectionRepository;
            _projectItemRepository = projectItemRepository;
            _projectItemBlueprintRepository = projectItemBlueprintRepository;
            _projectQuestionRepository = projectQuestionRepository;
            _projectCollectionArtworkRepository = projectCollectionArtworkRepository;
            _imageService = imageService;
        }

        [HttpGet("get-by-id")]
        public async Task<IActionResult> GetById([FromQuery] Guid id)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (id == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Project ID is required." });

            try
            {
                var project = await _projectRepository.GetByIdAsync(id, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                return Json(new ApiResponse { success = true, data = project });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("get-all")]
        public async Task<IActionResult> GetAll()
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            try
            {
                var projects = await _projectRepository.GetAllAsync(userId);
                var projectIds = projects.Select(p => p.Id).ToArray();
                var artwork = await _projectCollectionArtworkRepository.FilterByProjectIdsAsync(projectIds, 5);
                var artworkByProject = artwork.ToLookup(a => a.ProjectId);

                var result = projects.Select(p => new ProjectListItem
                {
                    Id = p.Id,
                    Title = p.Title,
                    Description = p.Description,
                    Key = p.Key,
                    Color = p.Color,
                    Status = p.Status,
                    Created = p.Created,
                    Artwork = artworkByProject[p.Id].ToList()
                }).ToList();

                return Json(new ApiResponse { success = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] CreateProjectRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            try
            {
                if (string.IsNullOrWhiteSpace(request.Title))
                    return Json(new ApiResponse { success = false, message = "Title is required." });

                if (string.IsNullOrWhiteSpace(request.Key))
                    return Json(new ApiResponse { success = false, message = "Key is required." });

                if (!System.Text.RegularExpressions.Regex.IsMatch(request.Key, @"^[a-zA-Z0-9-]+$"))
                    return Json(new ApiResponse { success = false, message = "Key can only contain letters, numbers, and dashes." });

                var existing = await _projectRepository.GetByKeyAsync(request.Key);
                if (existing != null)
                    return Json(new ApiResponse { success = false, message = "A project with that key already exists." });

                var project = new Project
                {
                    AppUserId = userId,
                    Title = request.Title.Trim(),
                    Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                    Key = request.Key.Trim().ToLower(),
                    Color = string.IsNullOrWhiteSpace(request.Color) ? "#000000" : request.Color.Trim()
                };

                var created = await _projectRepository.CreateAsync(project);
                return Json(new ApiResponse { success = true, data = created });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("update-title")]
        public async Task<IActionResult> UpdateTitle([FromBody] UpdateProjectTitleRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.Id == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Project ID is required." });

            if (string.IsNullOrWhiteSpace(request.Title))
                return Json(new ApiResponse { success = false, message = "Title is required." });

            try
            {
                var project = await _projectRepository.GetByIdAsync(request.Id, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                project.Title = request.Title.Trim();
                await _projectRepository.UpdateAsync(project);
                return Json(new ApiResponse { success = true, data = project });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("update-key")]
        public async Task<IActionResult> UpdateKey([FromBody] UpdateProjectKeyRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.Id == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Project ID is required." });

            if (string.IsNullOrWhiteSpace(request.Key))
                return Json(new ApiResponse { success = false, message = "Key is required." });

            var cleanKey = request.Key.Trim().ToLower();
            if (!System.Text.RegularExpressions.Regex.IsMatch(cleanKey, @"^[a-zA-Z0-9-]+$"))
                return Json(new ApiResponse { success = false, message = "Key can only contain letters, numbers, and dashes." });

            try
            {
                var project = await _projectRepository.GetByIdAsync(request.Id, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                if (!string.Equals(project.Key, cleanKey, StringComparison.OrdinalIgnoreCase))
                {
                    var existing = await _projectRepository.GetByKeyAsync(cleanKey);
                    if (existing != null && existing.Id != request.Id)
                        return Json(new ApiResponse { success = false, message = "A project with that key already exists." });
                }

                project.Key = cleanKey;
                await _projectRepository.UpdateAsync(project);
                return Json(new ApiResponse { success = true, data = project });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

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

                var collections = await _projectCollectionRepository.GetByProjectIdAsync(projectId);
                return Json(new ApiResponse { success = true, data = collections });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

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
                    BlueprintNames = blueprints.Where(b => b.ItemId == i.Id).Select(b => b.Name).ToList()
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
                    Index = nextIndex
                };
                var created = await _projectItemRepository.CreateAsync(item);

                return Json(new ApiResponse
                {
                    success = true,
                    data = new ProjectItemListItem
                    {
                        Id = created.Id,
                        ProjectId = created.ProjectId,
                        Index = created.Index,
                        BlueprintNames = new List<string>()
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

                await _projectItemRepository.DeleteAsync(request.Id);
                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

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

        [HttpGet("get-questions")]
        public async Task<IActionResult> GetQuestions([FromQuery] Guid projectId)
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

                var questions = await _projectQuestionRepository.GetByProjectIdAsync(projectId);
                return Json(new ApiResponse { success = true, data = questions });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("create-question")]
        public async Task<IActionResult> CreateQuestion([FromBody] CreateProjectQuestionRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.ProjectId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Project ID is required." });

            if (string.IsNullOrWhiteSpace(request.Question))
                return Json(new ApiResponse { success = false, message = "Question is required." });

            try
            {
                var project = await _projectRepository.GetByIdAsync(request.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                var question = new ProjectQuestion
                {
                    ProjectId = request.ProjectId,
                    Question = request.Question.Trim(),
                    Index = request.Index
                };
                var created = await _projectQuestionRepository.CreateAsync(question);
                return Json(new ApiResponse { success = true, data = created });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("update-question")]
        public async Task<IActionResult> UpdateQuestion([FromBody] UpdateProjectQuestionRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.Id == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Question ID is required." });

            if (string.IsNullOrWhiteSpace(request.Question))
                return Json(new ApiResponse { success = false, message = "Question is required." });

            try
            {
                var question = await _projectQuestionRepository.GetByIdAsync(request.Id);
                if (question == null)
                    return Json(new ApiResponse { success = false, message = "Question not found." });

                var project = await _projectRepository.GetByIdAsync(question.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                question.Question = request.Question.Trim();
                await _projectQuestionRepository.UpdateAsync(question);
                return Json(new ApiResponse { success = true, data = question });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("delete-question")]
        public async Task<IActionResult> DeleteQuestion([FromBody] DeleteProjectQuestionRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.Id == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Question ID is required." });

            try
            {
                var question = await _projectQuestionRepository.GetByIdAsync(request.Id);
                if (question == null)
                    return Json(new ApiResponse { success = false, message = "Question not found." });

                var project = await _projectRepository.GetByIdAsync(question.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                await _projectQuestionRepository.DeleteAsync(request.Id);
                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("get-artwork")]
        public async Task<IActionResult> GetArtwork([FromQuery] Guid projectId, [FromQuery] Guid? collectionId = null, [FromQuery] int start = 0, [FromQuery] int length = 5)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (projectId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Project ID is required." });

            if (start < 0)
                return Json(new ApiResponse { success = false, message = "Start must be at least 0." });

            if (length < 1)
                return Json(new ApiResponse { success = false, message = "Length must be at least 1." });

            try
            {
                var project = await _projectRepository.GetByIdAsync(projectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                var artwork = await _projectCollectionArtworkRepository.FilterByProjectIdAsync(projectId, collectionId, start, length);
                return Json(new ApiResponse { success = true, data = artwork });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("collection/{collectionId}/artwork/{artworkId}/{index}")]
        public async Task<IActionResult> GetCollectionArtwork(Guid collectionId, Guid artworkId, int index)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (index < 1)
                return Json(new ApiResponse { success = false, message = "Index must be at least 1." });

            try
            {
                var artwork = await _projectCollectionArtworkRepository.GetByIdAsync(collectionId, artworkId);
                if (artwork == null)
                    return NotFound();

                var project = await _projectRepository.GetByIdAsync(artwork.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                var bytes = await _imageService.GetProjectCollectionArtworkAsync(collectionId, artworkId, index);
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
