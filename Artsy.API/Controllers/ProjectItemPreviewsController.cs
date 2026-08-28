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

                if (request.ModelId <= 0)
                    return Json(new ApiResponse { success = false, message = "Model ID is required." });

                var genModel = await _imageGenerationModelRepository.GetByIdAsync(request.ModelId);
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

                // Build question lookup for both project and item questions
                var projectQuestions = await _projectQuestionRepository.GetByProjectIdAsync(item.ProjectId);
                var itemQuestions = await _projectItemQuestionRepository.GetByItemIdAsync(request.ItemId);
                var questionLookup = new Dictionary<Guid, string>();
                foreach (var q in projectQuestions)
                    questionLookup[q.Id] = q.Question;
                foreach (var q in itemQuestions)
                    questionLookup[q.Id] = q.Question;

                // Load saved collection answers (project + item answers) when collectionId is provided
                if (request.CollectionId.HasValue && request.CollectionId.Value != Guid.Empty)
                {
                    var collectionAnswers = await _projectCollectionAnswerRepository.GetByCollectionIdAsync(request.CollectionId.Value);
                    if (collectionAnswers != null && collectionAnswers.Any())
                    {
                        foreach (var ans in collectionAnswers)
                        {
                            if (string.IsNullOrWhiteSpace(ans.Answer) || !ans.QuestionId.HasValue) continue;
                            // Only include answers for this item or project-level answers (no ItemId)
                            if (ans.ItemId.HasValue && ans.ItemId.Value != request.ItemId) continue;
                            if (questionLookup.TryGetValue(ans.QuestionId.Value, out var questionText))
                            {
                                promptBuilder.AppendLine();
                                promptBuilder.AppendLine($"Question: {questionText}");
                                promptBuilder.AppendLine($"Answer: {ans.Answer}");
                            }
                        }
                    }
                }

                // Also include any answers passed directly in the request
                if (request.Answers != null && request.Answers.Count > 0)
                {
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

                // For pattern design mode, append seamless repeating pattern instructions to the prompt
                if (string.Equals(request.Design, "pattern", StringComparison.OrdinalIgnoreCase))
                {
                    finalPrompt += ". Design this as a seamless repeating pattern that tiles perfectly without visible seams or borders. The artwork should be a continuous tileable pattern that can be repeated horizontally and vertically.";
                }

                // Append chroma key background instruction if OpacityJson has chroma keys
                var opacitySettings = _opacityService.ParseOpacityJson(artwork.OpacityJson);
                if (opacitySettings != null && opacitySettings.ChromaKeys.Count > 0)
                {
                    var firstColor = opacitySettings.ChromaKeys[0];
                    var hexColor = $"#{firstColor.R:X2}{firstColor.G:X2}{firstColor.B:X2}";
                    finalPrompt += $" the background for this image must be a solid color using {hexColor} hex color so that we can apply a chroma key to the image later";
                }

                // Append optional user-provided prompt at the very bottom
                if (!string.IsNullOrWhiteSpace(artwork.OptionalPrompt))
                    finalPrompt += $" {artwork.OptionalPrompt.Trim()}";

                modelRequest.Prompt = finalPrompt;
                // Use the artwork's aspect ratio to determine preview dimensions at 1K
                var (previewW, previewH) = ImageGenerationForOpenAI.GetDimensionsFromAspectRatio(artwork.AspectRatio, 1);
                modelRequest.Size = $"{previewW}x{previewH}";
                modelRequest.Quality = "low";

                var references = await _projectItemReferenceRepository.GetByItemIdAsync(request.ItemId);
                var inputImages = new List<byte[]>();
                var inputImageRefs = new List<object>();
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
                        else if (reference.CustomImageId.HasValue)
                        {
                            var customImg = await _customImageRepository.GetByIdAsync(reference.CustomImageId.Value);
                            if (customImg != null)
                            {
                                imageBytes = await _imageService.GetCustomImageAsync(customImg.AppUserId, customImg.Id, customImg.Extension);
                            }
                        }

                        if (imageBytes != null && imageBytes.Length > 0)
                        {
                            inputImages.Add(imageBytes);
                            inputImageRefs.Add(new { type = reference.ArtworkId.HasValue ? "artwork" : "custom", id = (reference.ArtworkId ?? reference.CustomImageId).ToString() });
                        }
                    }
                }

                // Load collection artwork references (custom images added in the collection wizard)
                if (request.CollectionId.HasValue && request.CollectionId.Value != Guid.Empty)
                {
                    var collectionRefs = await _projectCollectionArtworkReferenceRepository.GetByCollectionAndItemIdAsync(request.CollectionId.Value, request.ItemId);
                    if (collectionRefs != null && collectionRefs.Any())
                    {
                        foreach (var colRef in collectionRefs)
                        {
                            var customImg = await _customImageRepository.GetByIdAsync(colRef.CustomImageId);
                            if (customImg == null) continue;

                            var rawBytes = await _imageService.GetCustomImageAsync(customImg.AppUserId, customImg.Id, customImg.Extension);
                            if (rawBytes == null || rawBytes.Length == 0) continue;

                            inputImages.Add(rawBytes);
                            inputImageRefs.Add(new { type = "custom", id = customImg.Id.ToString() });
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
                    var imageGen = _imageGenerations.FirstOrDefault(g => g.ModelKey.Equals(genModel.ModelKey, StringComparison.OrdinalIgnoreCase));
                    if (imageGen == null)
                        throw new InvalidOperationException($"Image model '{genModel.ModelKey}' is not supported.");

                    var genRequest = new ImageGenerationRequest
                    {
                        Model = genModel.Model,
                        Prompt = finalPrompt,
                        InputImages = inputImages,
                        Width = previewW,
                        Height = previewH,
                        Quality = "low"
                    };

                    var previewInputDimensions = new List<(int width, int height)>();
                    foreach (var img in inputImages)
                    {
                        var dims = await _imageService.GetImageDimensionsAsync(img);
                        if (dims.HasValue)
                            previewInputDimensions.Add(dims.Value);
                    }

                    var previewTokenCost = _tokenCostOptions.Cost > 0 ? _tokenCostOptions.Cost : 0.01m;
                    var previewTokenizer = imageGen.CreateTokenizer(genModel);
                    var previewTokenCalc = previewTokenizer.CalculateTokens(finalPrompt, previewW, previewH, "low", previewInputDimensions, "auto", previewTokenCost);

                    if (!await _aiTokenService.UseTokensAsync(userId, previewTokenCalc.PlatformTokens))
                        throw new InvalidOperationException("Not enough tokens to generate the preview. Please purchase more tokens before continuing.");

                    var genResult = await imageGen.GenerateAsync(genRequest);
                    await _imageService.SaveProjectItemPreviewAsync(createdPreview.ProjectId, createdPreview.ItemId, createdPreview.Id, genResult.ImageBytes);

                    await _projectImageGenerationRepository.CreateAsync(new ProjectImageGeneration
                    {
                        ProjectId = item.ProjectId,
                        ItemId = request.ItemId,
                        AppUserId = userId,
                        ImageGenerationId = genModel.Id,
                        InputTextTokens = genResult.InputTokens,
                        InputImageTokens = 0,
                        OutputTokens = genResult.OutputTokens,
                        Tokens = previewTokenCalc.PlatformTokens,
                        Prompt = finalPrompt,
                        Filename = $"{createdPreview.Id}.jpg",
                        Resolution = $"{previewW}x{previewH}",
                        InputImages = inputImages.Count,
                        InputImageJson = System.Text.Json.JsonSerializer.Serialize(inputImageRefs),
                        Type = 0,
                        Cost = (int)Math.Round(previewTokenCalc.EstimatedCostUSD * 100)
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
