using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Artsy.API.Models;
using Artsy.API.Models.Projects;
using Artsy.API.Services;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Entities;
using Artsy.Data.Interfaces.Projects;
using Artsy.Data.Interfaces;

namespace Artsy.API.Controllers
{
    [Route("/api/projects")]
    [Authorize]
    public partial class ProjectsController : ApiController
    {
        readonly IProjectRepository _projectRepository;
        readonly IProjectCollectionRepository _projectCollectionRepository;
        readonly IProjectItemRepository _projectItemRepository;
        readonly IProjectBlueprintsRepository _projectBlueprintRepository;
        readonly IProjectItemArtworkRepository _projectItemArtworkRepository;
        readonly IProjectItemQuestionRepository _projectItemQuestionRepository;
        readonly IProjectQuestionRepository _projectQuestionRepository;
        readonly IProjectCollectionArtworkRepository _projectCollectionArtworkRepository;
        readonly IProjectItemPreviewRepository _projectItemPreviewRepository;
        readonly IProjectItemReferenceRepository _projectItemReferenceRepository;
        readonly IPrintifyBlueprintVariantRepository _variantRepository;
        readonly IPrintifyBlueprintVariantPlaceholderRepository _placeholderRepository;
        readonly IImageService _imageService;
        readonly IImageGeneration _imageGeneration;

        public ProjectsController(
            IProjectRepository projectRepository,
            IProjectCollectionRepository projectCollectionRepository,
            IProjectItemRepository projectItemRepository,
            IProjectBlueprintsRepository projectBlueprintRepository,
            IProjectItemArtworkRepository projectItemArtworkRepository,
            IProjectItemQuestionRepository projectItemQuestionRepository,
            IProjectQuestionRepository projectQuestionRepository,
            IProjectCollectionArtworkRepository projectCollectionArtworkRepository,
            IProjectItemPreviewRepository projectItemPreviewRepository,
            IProjectItemReferenceRepository projectItemReferenceRepository,
            IPrintifyBlueprintVariantRepository variantRepository,
            IPrintifyBlueprintVariantPlaceholderRepository placeholderRepository,
            IImageService imageService,
            IImageGeneration imageGeneration)
        {
            _projectRepository = projectRepository;
            _projectCollectionRepository = projectCollectionRepository;
            _projectItemRepository = projectItemRepository;
            _projectBlueprintRepository = projectBlueprintRepository;
            _projectItemArtworkRepository = projectItemArtworkRepository;
            _projectItemQuestionRepository = projectItemQuestionRepository;
            _projectQuestionRepository = projectQuestionRepository;
            _projectCollectionArtworkRepository = projectCollectionArtworkRepository;
            _projectItemPreviewRepository = projectItemPreviewRepository;
            _projectItemReferenceRepository = projectItemReferenceRepository;
            _variantRepository = variantRepository;
            _placeholderRepository = placeholderRepository;
            _imageService = imageService;
            _imageGeneration = imageGeneration;
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

        [HttpPost("update-publish-to-printify")]
        public async Task<IActionResult> UpdatePublishToPrintify([FromBody] UpdateProjectPublishToPrintifyRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.Id == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Project ID is required." });

            try
            {
                var project = await _projectRepository.GetByIdAsync(request.Id, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                project.PublishToPrintify = request.PublishToPrintify;
                await _projectRepository.UpdateAsync(project);
                return Json(new ApiResponse { success = true, data = project });
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

        [HttpGet("get-checklist")]
        public async Task<IActionResult> GetChecklist([FromQuery] Guid projectId)
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

                var items = (await _projectItemRepository.GetByProjectIdAsync(projectId)).ToList();
                var itemIds = items.Select(i => i.Id).ToList();
                var artwork = await _projectItemArtworkRepository.GetByProjectIdAsync(projectId);
                var blueprints = await _projectBlueprintRepository.GetByProjectIdAsync(projectId);
                var itemQuestions = await _projectItemQuestionRepository.GetByProjectIdAsync(projectId);
                var questions = await _projectQuestionRepository.GetByProjectIdAsync(projectId);
                var collections = (await _projectCollectionRepository.GetByProjectIdAsync(projectId)).ToList();

                var artworkList = artwork.ToList();
                var customItemIds = artworkList.Where(a => a.ArtworkType == "custom").Select(a => a.ItemId).ToHashSet();
                var aiItems = items.Where(i => !customItemIds.Contains(i.Id)).ToList();

                var imageGenerationSetupCompleted = aiItems.Count(item =>
                {
                    var itemArtwork = artworkList.FirstOrDefault(a => a.ItemId == item.Id);
                    return itemArtwork != null &&
                           !string.IsNullOrWhiteSpace(itemArtwork.Prompt);
                });
                var imageGenerationSetup = aiItems.Count > 0 && imageGenerationSetupCompleted == aiItems.Count;

                var productBlueprintsAddedCompleted = blueprints.Count(b => !string.IsNullOrWhiteSpace(b.BlueprintJson));
                var productBlueprintsAdded = productBlueprintsAddedCompleted > 0;

                var validItemQuestions = itemQuestions.Where(q => !string.IsNullOrWhiteSpace(q.Question)).ToList();
                var itemQuestionsAddedCompleted = validItemQuestions.Count;
                var itemQuestionsWithoutQuestion = aiItems.Count(item => !validItemQuestions.Any(q => q.ItemId == item.Id));
                var itemQuestionsAdded = aiItems.Count > 0 && itemQuestionsWithoutQuestion == 0;

                var questionCount = questions.Count(q => !string.IsNullOrWhiteSpace(q.Question));
                var questionsAdded = questionCount > 0;

                var result = new ProjectChecklistResponse
                {
                    ImageGenerationSetup = imageGenerationSetup,
                    ImageGenerationSetupCompleted = imageGenerationSetupCompleted,
                    ImageGenerationSetupTotal = Math.Max(1, aiItems.Count),
                    ItemQuestionsAdded = itemQuestionsAdded,
                    ItemQuestionsAddedCompleted = itemQuestionsAddedCompleted,
                    ItemQuestionsAddedTotal = Math.Max(1, itemQuestionsWithoutQuestion + itemQuestionsAddedCompleted),
                    ProductBlueprintsAdded = productBlueprintsAdded,
                    ProductBlueprintsAddedCompleted = productBlueprintsAddedCompleted,
                    ProductBlueprintsAddedTotal = Math.Max(1, items.Count),
                    QuestionsAdded = questionsAdded,
                    QuestionsAddedCompleted = questionCount > 0 ? questionCount : 0,
                    QuestionsAddedTotal = Math.Max(1, questionCount),
                    CollectionsAdded = collections.Count > 0,
                    CollectionsAddedCompleted = collections.Count,
                    CollectionsAddedTotal = Math.Max(1, collections.Count)
                };

                return Json(new ApiResponse { success = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }
    }
}
