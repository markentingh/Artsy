using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Artsy.API.Models;
using Artsy.API.Models.Projects;
using Artsy.API.Services;
using Artsy.Data.Entities;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces;
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

            if (request.ItemId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Item ID is required." });

            try
            {
                var item = await _projectItemRepository.GetByIdAsync(request.ItemId);
                if (item == null)
                    return Json(new ApiResponse { success = false, message = "Item not found." });

                var project = await _projectRepository.GetByIdAsync(item.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                var artworkList = await _projectItemArtworkRepository.GetByItemIdAsync(request.ItemId);
                var artwork = artworkList.FirstOrDefault();
                if (artwork == null || string.IsNullOrWhiteSpace(artwork.ImageModel))
                    return Json(new ApiResponse { success = false, message = "No image model configured for this item." });

                var genModel = await _imageGenerationModelRepository.GetByModelKeyAsync(artwork.ImageModel);
                if (genModel == null)
                    return Json(new ApiResponse { success = false, message = "Image model not found in database." });

                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                };

                var modelRequest = new OpenAIImageRequest();
                modelRequest.Model = genModel.Model;

                var promptBuilder = new StringBuilder(artwork.Prompt ?? "");

                if (request.Answers != null && request.Answers.Count > 0)
                {
                    var projectQuestions = await _projectQuestionRepository.GetByProjectIdAsync(item.ProjectId);
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
                modelRequest.Size = "1024x1024";
                modelRequest.Quality = "low";

                var references = await _projectItemReferenceRepository.GetByItemIdAsync(request.ItemId);
                var inputImages = new List<byte[]>();
                if (references != null && references.Any())
                {
                    foreach (var reference in references)
                    {
                        byte[]? imageBytes = null;

                        if (reference.ArtworkId.HasValue)
                        {
                            var refPreviews = await _projectItemPreviewRepository.GetByItemIdAsync(reference.ArtworkId.Value);
                            var newestPreview = refPreviews.FirstOrDefault();
                            if (newestPreview != null)
                            {
                                imageBytes = await _imageService.GetProjectItemPreviewAsync(reference.ProjectId, reference.ArtworkId.Value, newestPreview.Id);
                            }
                        }
                        else
                        {
                            imageBytes = await _imageService.GetProjectItemReferenceAsync(reference.ProjectId, reference.Id, reference.Extension);
                        }

                        if (imageBytes != null && imageBytes.Length > 0)
                        {
                            inputImages.Add(imageBytes);
                        }
                    }
                }

                var imageModelJson = JsonSerializer.Serialize(modelRequest, jsonOptions);

                var preview = new ProjectItemPreview
                {
                    ProjectId = item.ProjectId,
                    ItemId = request.ItemId,
                    ImageModel = artwork.ImageModel,
                    ImageModelJson = imageModelJson
                };
                var createdPreview = await _projectItemPreviewRepository.CreateAsync(preview);

                try
                {
                    var imageGen = _imageGenerations.FirstOrDefault(g => g.ModelKey.Equals(artwork.ImageModel, StringComparison.OrdinalIgnoreCase));
                    if (imageGen == null)
                        throw new InvalidOperationException($"Image model '{artwork.ImageModel}' is not supported.");

                    var genRequest = new ImageGenerationRequest
                    {
                        Model = genModel.Model,
                        Prompt = finalPrompt,
                        InputImages = inputImages,
                        Width = 1024,
                        Height = 1024,
                        Quality = "low"
                    };
                    var genResult = await imageGen.GenerateAsync(genRequest);
                    await _imageService.SaveProjectItemPreviewAsync(createdPreview.ProjectId, createdPreview.ItemId, createdPreview.Id, genResult.ImageBytes);

                    await _projectImageGenerationRepository.CreateAsync(new ProjectImageGeneration
                    {
                        ProjectId = item.ProjectId,
                        ItemId = request.ItemId,
                        InputTextTokens = genResult.InputTokens,
                        InputImageTokens = 0,
                        OutputTokens = genResult.OutputTokens,
                        ImageModel = genModel.Model,
                        Prompt = finalPrompt,
                        Filename = $"{createdPreview.Id}.jpg"
                    });
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

        [HttpPost("delete-item-preview")]
        public async Task<IActionResult> DeleteItemPreview([FromBody] DeleteItemPreviewRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.Id == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Preview ID is required." });

            try
            {
                var preview = await _projectItemPreviewRepository.GetByIdAsync(request.Id);
                if (preview == null)
                    return Json(new ApiResponse { success = false, message = "Preview not found." });

                var project = await _projectRepository.GetByIdAsync(preview.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                await _imageService.DeleteProjectItemPreviewAsync(preview.ProjectId, preview.ItemId, preview.Id);
                await _projectItemPreviewRepository.DeleteAsync(request.Id);

                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }
    }

    public class DeleteItemPreviewRequest
    {
        public Guid Id { get; set; }
    }
}
