using System.Text;
using System.Text.Json;
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
        [HttpGet("get-item-previews")]
        public async Task<IActionResult> GetItemPreviews([FromQuery] Guid itemId)
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

                var previews = await _projectItemPreviewRepository.GetByItemIdAsync(itemId);
                return Json(new ApiResponse { success = true, data = previews });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("generate-item-preview")]
        public async Task<IActionResult> GenerateItemPreview([FromBody] GenerateProjectItemPreviewRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.ProjectId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Project ID is required." });

            if (request.ItemId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Item ID is required." });

            if (string.IsNullOrWhiteSpace(request.ImageModel))
                return Json(new ApiResponse { success = false, message = "Image model is required." });

            try
            {
                var project = await _projectRepository.GetByIdAsync(request.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                var item = await _projectItemRepository.GetByIdAsync(request.ItemId);
                if (item == null || item.ProjectId != request.ProjectId)
                    return Json(new ApiResponse { success = false, message = "Item not found." });

                var imageModelJson = request.ImageModelJson ?? "{}";
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                };

                var modelRequest = JsonSerializer.Deserialize<OpenAIImageRequest>(imageModelJson, jsonOptions);
                if (modelRequest == null)
                    modelRequest = new OpenAIImageRequest();

                var promptBuilder = new StringBuilder(modelRequest.Prompt ?? "");

                if (request.Answers != null && request.Answers.Count > 0)
                {
                    var projectQuestions = await _projectQuestionRepository.GetByProjectIdAsync(request.ProjectId);
                    var itemQuestions = await _projectItemQuestionRepository.GetByItemIdAsync(request.ItemId);
                    var questionLookup = new Dictionary<Guid, string>();
                    foreach (var q in projectQuestions)
                        questionLookup[q.Id] = q.Question;
                    foreach (var q in itemQuestions)
                        questionLookup[q.Id] = q.Question;

                    foreach (var answer in request.Answers)
                    {
                        if (string.IsNullOrWhiteSpace(answer.Answer))
                            continue;
                        if (questionLookup.TryGetValue(answer.QuestionId, out var questionText))
                        {
                            promptBuilder.AppendLine();
                            promptBuilder.AppendLine($"Question: {questionText}");
                            promptBuilder.AppendLine($"Answer: {answer.Answer}");
                        }
                    }
                }

                var finalPrompt = promptBuilder.ToString().Trim();
                if (string.IsNullOrWhiteSpace(finalPrompt))
                    return Json(new ApiResponse { success = false, message = "Prompt is required to generate a preview." });

                modelRequest.Prompt = finalPrompt;

                var references = await _projectItemReferenceRepository.GetByItemIdAsync(request.ItemId);
                if (references != null && references.Any())
                {
                    modelRequest.Images = new List<OpenAIImageReference>();
                    foreach (var reference in references)
                    {
                        var imageBytes = await _imageService.GetProjectItemReferenceAsync(reference.ProjectId, reference.Id, reference.Extension);
                        if (imageBytes != null && imageBytes.Length > 0)
                        {
                            modelRequest.Images.Add(new OpenAIImageReference
                            {
                                Image = Convert.ToBase64String(imageBytes),
                                Detail = "auto"
                            });
                        }
                    }
                }

                imageModelJson = JsonSerializer.Serialize(modelRequest, jsonOptions);

                var preview = new ProjectItemPreview
                {
                    ProjectId = request.ProjectId,
                    ItemId = request.ItemId,
                    ImageModel = request.ImageModel,
                    ImageModelJson = imageModelJson
                };
                var createdPreview = await _projectItemPreviewRepository.CreateAsync(preview);

                try
                {
                    var imageBytes = await _imageGeneration.GenerateAsync(request.ImageModel, imageModelJson, "low");
                    await _imageService.SaveProjectItemPreviewAsync(createdPreview.ProjectId, createdPreview.ItemId, createdPreview.Id, imageBytes);
                }
                catch
                {
                    await _projectItemPreviewRepository.DeleteAsync(createdPreview.Id);
                    throw;
                }

                return Json(new ApiResponse { success = true, data = createdPreview });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("item/{itemId}/preview/{previewId}")]
        public async Task<IActionResult> GetItemPreview(Guid itemId, Guid previewId, [FromQuery] bool thumb = false)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (itemId == Guid.Empty || previewId == Guid.Empty)
                return NotFound();

            try
            {
                var preview = await _projectItemPreviewRepository.GetByIdAsync(previewId);
                if (preview == null || preview.ItemId != itemId)
                    return NotFound();

                var project = await _projectRepository.GetByIdAsync(preview.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                var bytes = await _imageService.GetProjectItemPreviewAsync(preview.ProjectId, preview.ItemId, preview.Id, thumb);
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
