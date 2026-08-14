using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Artsy.API.Models;
using Artsy.API.Models.Projects;
using Artsy.API.Models.Collections;
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
        [HttpPost("create-collection")]
        public async Task<IActionResult> CreateCollection([FromBody] CreateCollectionRequest request)
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

                var collection = new ProjectCollection
                {
                    ProjectId = request.ProjectId,
                    Title = string.IsNullOrWhiteSpace(request.Title) ? $"Collection {DateTime.UtcNow:yyyy-MM-dd}" : request.Title.Trim()
                };
                var created = await _projectCollectionRepository.CreateAsync(collection);
                return Json(new ApiResponse { success = true, data = created });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("get-collection-answers")]
        public async Task<IActionResult> GetCollectionAnswers([FromQuery] Guid collectionId)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (collectionId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Collection ID is required." });

            try
            {
                var answers = await _projectCollectionAnswerRepository.GetByCollectionIdAsync(collectionId);
                return Json(new ApiResponse
                {
                    success = true,
                    data = answers.Select(a => new
                    {
                        id = a.Id,
                        questionId = a.QuestionId,
                        itemId = a.ItemId,
                        answer = a.Answer
                    })
                });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("get-collection-artwork")]
        public async Task<IActionResult> GetCollectionArtwork([FromQuery] Guid collectionId)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (collectionId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Collection ID is required." });

            try
            {
                var artwork = await _projectCollectionArtworkRepository.GetByCollectionIdAsync(collectionId);
                return Json(new ApiResponse
                {
                    success = true,
                    data = artwork.Select(a => new
                    {
                        id = a.Id,
                        itemId = a.ItemId,
                        active = a.Active,
                        accepted = a.Accepted,
                        fullSize = a.FullSize,
                        imageModel = a.ImageModel,
                        width = a.Width,
                        height = a.Height,
                        index = a.Index,
                        printifyImageId = a.PrintifyImageId,
                        opacity = a.Opacity
                    })
                });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("save-collection-draft")]
        public async Task<IActionResult> SaveCollectionDraft([FromBody] SaveCollectionDraftRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.ProjectId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Project ID is required." });

            if (request.CollectionId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Collection ID is required." });

            try
            {
                var project = await _projectRepository.GetByIdAsync(request.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                var collection = await _projectCollectionRepository.GetByIdAsync(request.CollectionId);
                if (collection == null || collection.ProjectId != request.ProjectId)
                    return Json(new ApiResponse { success = false, message = "Collection not found." });

                if (request.Answers != null && request.Answers.Count > 0)
                {
                    foreach (var answer in request.Answers)
                    {
                        if (string.IsNullOrWhiteSpace(answer.Answer))
                            continue;

                        var entity = new ProjectCollectionAnswer
                        {
                            ProjectId = request.ProjectId,
                            CollectionId = request.CollectionId,
                            QuestionId = answer.QuestionId,
                            ItemId = answer.ItemId,
                            Answer = answer.Answer.Trim()
                        };
                        await _projectCollectionAnswerRepository.UpsertAsync(entity);
                    }
                }

                return Json(new ApiResponse { success = true, data = collection });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("generate-collection-artwork")]
        public async Task<IActionResult> GenerateCollectionArtwork([FromBody] GenerateCollectionArtworkRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.ProjectId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Project ID is required." });

            if (request.CollectionId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Collection ID is required." });

            if (request.ItemId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Item ID is required." });

            try
            {
                var project = await _projectRepository.GetByIdAsync(request.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                var item = await _projectItemRepository.GetByIdAsync(request.ItemId);
                if (item == null || item.ProjectId != request.ProjectId)
                    return Json(new ApiResponse { success = false, message = "Item not found." });

                var artworkList = await _projectItemArtworkRepository.GetByItemIdAsync(request.ItemId);
                var artwork = artworkList.FirstOrDefault();
                if (artwork == null || string.IsNullOrWhiteSpace(artwork.Prompt))
                    return Json(new ApiResponse { success = false, message = "No prompt configured for this item." });

                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                };

                if (request.ModelId == null || request.ModelId <= 0)
                    return Json(new ApiResponse { success = false, message = "Image model is required." });

                var genModel = await _imageGenerationModelRepository.GetByIdAsync(request.ModelId.Value);
                if (genModel == null)
                    return Json(new ApiResponse { success = false, message = "Image model not found in database." });

                var modelRequest = new OpenAIImageRequest();
                modelRequest.Model = genModel.Model;

                var promptBuilder = new StringBuilder(artwork.Prompt ?? "");

                if (request.Answers != null && request.Answers.Count > 0)
                {
                    var projectQuestions = await _projectQuestionRepository.GetByProjectIdAsync(request.ProjectId);
                    var itemQuestions = await _projectItemQuestionRepository.GetByItemIdAsync(request.ItemId);

                    var ignoredQuestionIds = new HashSet<Guid>();
                    if (!string.IsNullOrWhiteSpace(artwork.IgnoredQuestions))
                    {
                        try
                        {
                            var ignoredList = JsonSerializer.Deserialize<List<Guid>>(artwork.IgnoredQuestions);
                            if (ignoredList != null)
                                ignoredQuestionIds = new HashSet<Guid>(ignoredList);
                        }
                        catch { }
                    }

                    var questionLookup = new Dictionary<Guid, string>();
                    foreach (var q in projectQuestions)
                    {
                        if (ignoredQuestionIds.Contains(q.Id))
                            continue;
                        questionLookup[q.Id] = q.Question;
                    }
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

                if (!string.IsNullOrWhiteSpace(request.RequestedChanges))
                {
                    promptBuilder.AppendLine();
                    promptBuilder.AppendLine($"Requested Changes: {request.RequestedChanges}");
                }

                var finalPrompt = promptBuilder.ToString().Trim();
                if (string.IsNullOrWhiteSpace(finalPrompt))
                    return Json(new ApiResponse { success = false, message = "Prompt is required to generate artwork." });

                // Append chroma key background instruction if OpacityJson has chroma keys
                var opacitySettings = _opacityService.ParseOpacityJson(artwork.OpacityJson);
                if (opacitySettings != null && opacitySettings.ChromaKeys.Count > 0)
                {
                    var firstColor = opacitySettings.ChromaKeys[0];
                    var hexColor = $"#{firstColor.R:X2}{firstColor.G:X2}{firstColor.B:X2}";
                    finalPrompt += $" the background for this image must be a completely flat, uniform, solid color using {hexColor} hex color with no gradients, textures, shadows, or objects, filling the entire background area, so that we can apply a chroma key to the image later";
                }

                modelRequest.Prompt = finalPrompt;

                var blueprints = await _projectBlueprintRepository.GetByProjectIdAsync(request.ProjectId);
                var placementDims = new List<(int W, int H)>();
                foreach (var bp in blueprints)
                {
                    if (string.IsNullOrWhiteSpace(bp.PlacementJson)) continue;
                    try
                    {
                        var placements = JsonSerializer.Deserialize<List<PlacementDto>>(bp.PlacementJson);
                        if (placements == null) continue;
                        foreach (var p in placements)
                        {
                            if (p.GetItemId() == request.ItemId)
                                placementDims.Add(p.GetDimensions());
                        }
                    }
                    catch { }
                }

                int width, height;
                if (placementDims.Any())
                {
                    var maxDim = placementDims.Max(d => Math.Max(d.W, d.H));
                    if (maxDim <= 1024)
                    {
                        width = 1024;
                        height = 1024;
                    }
                    else
                    {
                        width = 2048;
                        height = 2048;
                    }
                }
                else
                {
                    width = request.Width > 0 ? request.Width : 2048;
                    height = request.Height > 0 ? request.Height : 2048;
                }

                modelRequest.Size = ImageGenerationForOpenAI.FindBestResolution($"{width}x{height}");
                modelRequest.Quality = "medium";

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
                            var refCollectionArtwork = await _projectCollectionArtworkRepository.GetByCollectionAndItemIdAsync(request.CollectionId, reference.ArtworkId.Value);
                            if (refCollectionArtwork != null)
                            {
                                imageBytes = await _imageService.GetProjectCollectionArtworkImageAsync(reference.ProjectId, request.CollectionId, reference.ArtworkId.Value, refCollectionArtwork.Id);
                                if (imageBytes == null || imageBytes.Length == 0)
                                {
                                    imageBytes = await _imageService.GetProjectCollectionArtworkFullSizeAsync(reference.ProjectId, request.CollectionId, reference.ArtworkId.Value, refCollectionArtwork.Id);
                                }
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

                var imageModelJson = JsonSerializer.Serialize(modelRequest, jsonOptions);

                var collectionArtwork = new ProjectCollectionArtwork
                {
                    ProjectId = request.ProjectId,
                    CollectionId = request.CollectionId,
                    ItemId = request.ItemId,
                    Active = true,
                    Width = width,
                    Height = height,
                    ImageModel = genModel.Model,
                    Prompt = finalPrompt,
                    Index = item.Index
                };
                var created = await _projectCollectionArtworkRepository.UpsertAsync(collectionArtwork);

                try
                {
                    var imageGen = _imageGenerations.FirstOrDefault(g => g.ModelKey.Equals(genModel.ModelKey, StringComparison.OrdinalIgnoreCase));
                    if (imageGen == null)
                        throw new InvalidOperationException($"Image model '{genModel.ModelKey}' is not supported.");

                    string? previousResponseId = null;
                    if (!string.IsNullOrWhiteSpace(request.RequestedChanges) && !string.IsNullOrWhiteSpace(created.ResponseId))
                    {
                        previousResponseId = created.ResponseId;
                    }

                    var genQuality = request.IsFullSize ? "high" : "medium";
                    var genRequest = new ImageGenerationRequest
                    {
                        Model = genModel.Model,
                        Prompt = finalPrompt,
                        InputImages = inputImages,
                        Width = width,
                        Height = height,
                        Quality = genQuality,
                        PreviousResponseId = previousResponseId,
                        UseResponsesApi = false
                    };

                    var inputImageDimensions = new List<(int width, int height)>();
                    foreach (var img in inputImages)
                    {
                        var dims = await _imageService.GetImageDimensionsAsync(img);
                        if (dims.HasValue)
                            inputImageDimensions.Add(dims.Value);
                    }

                    var tokenCost = _tokenCostOptions.Cost > 0 ? _tokenCostOptions.Cost : 0.01m;
                    var tokenizer = imageGen.CreateTokenizer(genModel);
                    var tokenCalc = tokenizer.CalculateTokens(finalPrompt, width, height, genQuality, inputImageDimensions, "auto", tokenCost);

                    if (!await _aiTokenService.UseTokensAsync(userId, tokenCalc.PlatformTokens))
                        throw new InvalidOperationException("Not enough tokens to generate the artwork. Please purchase more tokens before continuing.");

                    var genResult = await imageGen.GenerateAsync(genRequest);
                    if (request.IsFullSize)
                        await _imageService.SaveProjectCollectionArtworkFullSizeAsync(request.ProjectId, request.CollectionId, request.ItemId, created.Id, genResult.ImageBytes);
                    else
                        await _imageService.SaveProjectCollectionArtworkAsync(request.ProjectId, request.CollectionId, request.ItemId, created.Id, genResult.ImageBytes);

                    // Apply opacity mask (chroma key) processing if configured
                    if (opacitySettings != null && opacitySettings.ChromaKeys.Count > 0)
                    {
                        // Apply chroma keys to create transparent PNG
                        var pngBytes = await _opacityService.ApplyChromaKeysAsync(genResult.ImageBytes, opacitySettings);

                        // Save the chroma-only PNG before the overlay is applied
                        await _imageService.SaveProjectCollectionArtworkChromaAsync(request.ProjectId, request.CollectionId, request.ItemId, created.Id, pngBytes);

                        // Apply overlay color if set (tints all non-transparent pixels)
                        if (opacitySettings.Overlay != null && !string.IsNullOrWhiteSpace(opacitySettings.Overlay.Color))
                        {
                            pngBytes = await _opacityService.ApplyOverlayAsync(pngBytes, opacitySettings.Overlay.Color);
                        }

                        if (request.IsFullSize)
                            await _imageService.SaveProjectCollectionArtworkFullSizePngAsync(request.ProjectId, request.CollectionId, request.ItemId, created.Id, pngBytes);
                        else
                            await _imageService.SaveProjectCollectionArtworkPngAsync(request.ProjectId, request.CollectionId, request.ItemId, created.Id, pngBytes);

                        // Resolve background image bytes
                        byte[]? bgBytes = null;
                        string? bgColor = null;
                        if (opacitySettings.Background != null)
                        {
                            if (!string.IsNullOrWhiteSpace(opacitySettings.Background.Id))
                            {
                                try
                                {
                                    var bgId = Guid.Parse(opacitySettings.Background.Id);
                                    if (opacitySettings.Background.Type == "custom")
                                    {
                                        var customImg = await _customImageRepository.GetByIdAsync(bgId);
                                        if (customImg != null)
                                            bgBytes = await _imageService.GetCustomImageAsync(customImg.AppUserId, customImg.Id, customImg.Extension);
                                    }
                                    else if (opacitySettings.Background.Type == "artwork")
                                    {
                                        var bgCollectionArtwork = await _projectCollectionArtworkRepository.GetByCollectionAndItemIdAsync(request.CollectionId, bgId);
                                        if (bgCollectionArtwork != null)
                                        {
                                            bgBytes = await _imageService.GetProjectCollectionArtworkImageAsync(request.ProjectId, request.CollectionId, bgId, bgCollectionArtwork.Id);
                                            if (bgBytes == null || bgBytes.Length == 0)
                                                bgBytes = await _imageService.GetProjectCollectionArtworkFullSizeAsync(request.ProjectId, request.CollectionId, bgId, bgCollectionArtwork.Id);
                                        }
                                    }
                                }
                                catch { /* ignore background resolution errors, fall back to color or black */ }
                            }

                            // Fall back to the configured background color if no image was resolved
                            if (bgBytes == null && !string.IsNullOrWhiteSpace(opacitySettings.Background.Color))
                                bgColor = opacitySettings.Background.Color;
                        }

                        // Composite PNG over background to create JPG with background
                        var jpgWithBgBytes = await _opacityService.CompositeOverBackgroundAsync(pngBytes, bgBytes, bgColor);
                        await _imageService.SaveProjectCollectionArtworkJpgWithBgAsync(request.ProjectId, request.CollectionId, request.ItemId, created.Id, jpgWithBgBytes);

                        created.Opacity = true;
                    }

                    created.Active = true;
                    created.ResponseId = genResult.ResponseId ?? "";
                    if (request.IsFullSize)
                        created.FullSize = true;
                    await _projectCollectionArtworkRepository.UpdateAsync(created);

                    await _projectImageGenerationRepository.CreateAsync(new ProjectImageGeneration
                    {
                        ProjectId = request.ProjectId,
                        CollectionId = request.CollectionId,
                        ItemId = request.ItemId,
                        AppUserId = userId,
                        ImageGenerationId = genModel.Id,
                        InputTextTokens = genResult.InputTokens,
                        InputImageTokens = 0,
                        OutputTokens = genResult.OutputTokens,
                        Tokens = tokenCalc.PlatformTokens,
                        Prompt = finalPrompt,
                        Filename = request.IsFullSize ? $"{created.Id}_fullsize.jpg" : $"{created.Id}.jpg",
                        Resolution = $"{width}x{height}",
                        InputImages = inputImages.Count,
                        InputImageJson = System.Text.Json.JsonSerializer.Serialize(inputImageRefs),
                        Type = 1,
                        Cost = (int)Math.Round(tokenCalc.EstimatedCostUSD * 100)
                    });
                }
                catch (Exception genEx)
                {
                    return Json(new ApiResponse { success = false, message = genEx.Message });
                }

                return Json(new ApiResponse { success = true, data = created });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("upscale-artwork")]
        public async Task<IActionResult> UpscaleArtwork([FromBody] UpscaleArtworkRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.ProjectId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Project ID is required." });

            if (request.CollectionId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Collection ID is required." });

            if (request.ItemId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Item ID is required." });

            try
            {
                var project = await _projectRepository.GetByIdAsync(request.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                var artwork = await _projectCollectionArtworkRepository.GetByCollectionAndItemIdAsync(request.CollectionId, request.ItemId);
                if (artwork == null || !artwork.Active)
                    return Json(new ApiResponse { success = false, message = "No artwork found to upscale." });

                if (artwork.FullSize)
                    return Json(new ApiResponse { success = true, data = artwork });

                if (!await _aiTokenService.UseTokensAsync(userId, 2))
                    return Json(new ApiResponse { success = false, message = "Not enough tokens to generate the artwork. Please purchase more tokens before continuing." });

                byte[] previewBytes;
                if (artwork.Opacity)
                {
                    // For opacity artworks, upscale the transparent PNG, not the JPG
                    previewBytes = await _imageService.GetProjectCollectionArtworkPngAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id);
                }
                else
                {
                    previewBytes = await _imageService.GetProjectCollectionArtworkImageAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id);
                }
                if (previewBytes == null || previewBytes.Length == 0)
                    return Json(new ApiResponse { success = false, message = "Preview image data not found." });

                var upscaledBytes = await _imageUpscaler.UpscaleAsync(previewBytes);

                if (artwork.Opacity)
                {
                    // The upscaler returns the original background, so re-apply chroma/overlay to get a transparent PNG
                    var itemArtworkList = await _projectItemArtworkRepository.GetByItemIdAsync(request.ItemId);
                    var itemArtwork = itemArtworkList.FirstOrDefault();
                    var opacitySettings = _opacityService.ParseOpacityJson(itemArtwork?.OpacityJson) ?? new OpacitySettings();
                    var pngBytes = await _opacityService.ApplyChromaKeysAsync(upscaledBytes, opacitySettings);
                    if (!string.IsNullOrWhiteSpace(opacitySettings.Overlay?.Color))
                        pngBytes = await _opacityService.ApplyOverlayAsync(pngBytes, opacitySettings.Overlay.Color);

                    await _imageService.SaveProjectCollectionArtworkFullSizePngAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, pngBytes);
                }
                else
                {
                    await _imageService.SaveProjectCollectionArtworkFullSizeAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, upscaledBytes);
                }

                artwork.FullSize = true;
                await _projectCollectionArtworkRepository.UpdateAsync(artwork);

                await _projectImageUpscaleRepository.CreateAsync(new ProjectImageUpscale
                {
                    ProjectId = request.ProjectId,
                    CollectionId = request.CollectionId,
                    ItemId = request.ItemId,
                    ArtworkId = artwork.Id,
                    Width = artwork.Width * 2,
                    Height = artwork.Height * 2,
                    Scale = 2,
                    Created = DateTime.UtcNow
                });

                var upscaledImageModel = await _imageGenerationModelRepository.GetByModelKeyAsync(artwork.ImageModel);

                await _projectImageGenerationRepository.CreateAsync(new ProjectImageGeneration
                {
                    ProjectId = request.ProjectId,
                    CollectionId = request.CollectionId,
                    ItemId = request.ItemId,
                    AppUserId = userId,
                    ImageGenerationId = upscaledImageModel?.Id,
                    InputTextTokens = 0,
                    InputImageTokens = 0,
                    OutputTokens = 0,
                    Tokens = 2,
                    Prompt = "",
                    Filename = artwork.Opacity ? $"{artwork.Id}_fullsize.png" : $"{artwork.Id}_fullsize.jpg",
                    Resolution = $"{artwork.Width * 2}x{artwork.Height * 2}",
                    InputImages = 0,
                    InputImageJson = "[]",
                    Type = 3,
                    Cost = (int)Math.Round(2 * (_tokenCostOptions.Cost > 0 ? _tokenCostOptions.Cost : 0.01m) * 100)
                });

                return Json(new ApiResponse { success = true, data = artwork });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("delete-collection")]
        public async Task<IActionResult> DeleteCollection([FromBody] DeleteCollectionRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.Id == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Collection ID is required." });

            try
            {
                var collection = await _projectCollectionRepository.GetByIdAsync(request.Id);
                if (collection == null)
                    return Json(new ApiResponse { success = false, message = "Collection not found." });

                var project = await _projectRepository.GetByIdAsync(collection.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                await _projectCollectionRepository.DeleteAsync(request.Id);
                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("accept-collection-artwork")]
        public async Task<IActionResult> AcceptCollectionArtwork([FromBody] AcceptCollectionArtworkRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.CollectionId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Collection ID is required." });

            if (request.ItemId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Item ID is required." });

            try
            {
                await _projectCollectionArtworkRepository.AcceptAsync(request.CollectionId, request.ItemId);
                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("auto-accept-custom-artwork")]
        public async Task<IActionResult> AutoAcceptCustomArtwork([FromBody] AutoAcceptCustomArtworkRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.ProjectId == Guid.Empty || request.CollectionId == Guid.Empty || request.ItemId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Project ID, Collection ID, and Item ID are required." });

            try
            {
                var project = await _projectRepository.GetByIdAsync(request.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                var artworkList = await _projectItemArtworkRepository.GetByItemIdAsync(request.ItemId);
                var itemArtwork = artworkList.FirstOrDefault();
                if (itemArtwork == null || itemArtwork.ArtworkType != "custom" || !itemArtwork.CustomImageId.HasValue)
                    return Json(new ApiResponse { success = false, message = "Item does not have a custom image artwork." });

                var item = await _projectItemRepository.GetByIdAsync(request.ItemId);
                if (item == null)
                    return Json(new ApiResponse { success = false, message = "Item not found." });

                var customImage = await _customImageRepository.GetByIdAsync(itemArtwork.CustomImageId.Value);
                if (customImage == null)
                    return Json(new ApiResponse { success = false, message = "Custom image not found." });

                var referenceBytes = await _imageService.GetCustomImageAsync(customImage.AppUserId, customImage.Id, customImage.Extension);
                if (referenceBytes == null || referenceBytes.Length == 0)
                    return Json(new ApiResponse { success = false, message = "Custom reference image data not found." });

                var existing = await _projectCollectionArtworkRepository.GetByCollectionAndItemIdAsync(request.CollectionId, request.ItemId);
                if (existing != null && existing.Accepted && existing.FullSize)
                {
                    return Json(new ApiResponse { success = true, data = existing });
                }

                var collectionArtwork = new ProjectCollectionArtwork
                {
                    ProjectId = request.ProjectId,
                    CollectionId = request.CollectionId,
                    ItemId = request.ItemId,
                    Active = true,
                    Accepted = true,
                    FullSize = true,
                    Width = 2048,
                    Height = 2048,
                    ImageModel = "custom",
                    Prompt = "Custom image artwork",
                    Index = item.Index
                };
                var created = await _projectCollectionArtworkRepository.UpsertAsync(collectionArtwork);

                await _imageService.SaveProjectCollectionArtworkAsync(request.ProjectId, request.CollectionId, request.ItemId, created.Id, referenceBytes);
                await _imageService.SaveProjectCollectionArtworkFullSizeAsync(request.ProjectId, request.CollectionId, request.ItemId, created.Id, referenceBytes);

                return Json(new ApiResponse { success = true, data = created });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("delete-collection-artwork")]
        public async Task<IActionResult> DeleteCollectionArtwork([FromBody] DeleteCollectionArtworkRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.CollectionId == Guid.Empty || request.ItemId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Collection ID and Item ID are required." });

            try
            {
                await _projectCollectionArtworkRepository.DeleteAsync(request.CollectionId, request.ItemId);
                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("delete-product-image")]
        public async Task<IActionResult> DeleteProductImage([FromBody] DeleteProductImageRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.CollectionId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Collection ID is required." });

            try
            {
                await _projectCollectionProductImageRepository.SetInactiveAsync(
                    request.CollectionId, request.ProjectBlueprintId, request.ProductImageId);
                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("deactivate-product-images")]
        public async Task<IActionResult> DeactivateProductImages([FromBody] DeactivateProductImagesRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.CollectionId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Collection ID is required." });

            try
            {
                foreach (var combo in request.Combos ?? new List<DeleteProductImageRequest>())
                {
                    await _projectCollectionProductImageRepository.SetInactiveAsync(
                        request.CollectionId, combo.ProjectBlueprintId, combo.ProductImageId);
                }
                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("sync-product-image-selections")]
        public async Task<IActionResult> SyncProductImageSelections([FromBody] SyncProductImageSelectionsRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.CollectionId == Guid.Empty || request.ProjectId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Collection ID and Project ID are required." });

            try
            {
                var allImages = (await _projectCollectionProductImageRepository.GetAllByCollectionIdAsync(request.CollectionId)).ToList();

                var selectedKeys = new HashSet<string>(
                    request.SelectedCombos.Select(c => $"{c.ProjectBlueprintId}:{c.ProductImageId}")
                );

                foreach (var img in allImages)
                {
                    var key = $"{img.ProjectBlueprintId}:{img.ProductImageId}";
                    if (!selectedKeys.Contains(key) && img.Active)
                    {
                        await _projectCollectionProductImageRepository.SetInactiveAsync(
                            request.CollectionId, img.ProjectBlueprintId, img.ProductImageId);
                    }
                }

                foreach (var combo in request.SelectedCombos)
                {
                    var existing = allImages.FirstOrDefault(img =>
                        img.ProjectBlueprintId == combo.ProjectBlueprintId &&
                        img.ProductImageId == combo.ProductImageId);

                    if (existing == null)
                    {
                        await _projectCollectionProductImageRepository.CreateAsync(new ProjectCollectionProductImage
                        {
                            ProjectId = request.ProjectId,
                            CollectionId = request.CollectionId,
                            ProjectBlueprintId = combo.ProjectBlueprintId,
                            ProductImageId = combo.ProductImageId,
                            ImageModel = "",
                            Prompt = "",
                            Width = 0,
                            Height = 0,
                            Accepted = false,
                            ResponseId = "",
                            Active = true
                        });
                    }
                    else if (!existing.Active)
                    {
                        existing.Active = true;
                        await _projectCollectionProductImageRepository.UpdateAsync(existing);
                    }
                }

                var activeImages = await _projectCollectionProductImageRepository.GetByCollectionIdAsync(request.CollectionId);
                return Json(new ApiResponse
                {
                    success = true,
                    data = activeImages.Select(img => new
                    {
                        id = img.Id,
                        projectBlueprintId = img.ProjectBlueprintId,
                        productImageId = img.ProductImageId,
                        accepted = img.Accepted,
                        active = img.Active,
                        imageUrl = $"/api/projects/collection/{request.CollectionId}/product-image/{img.Id}"
                    })
                });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("collection/{collectionId}/item/{itemId}/artwork/{artworkId}")]
        public async Task<IActionResult> GetCollectionArtworkImage(Guid collectionId, Guid itemId, Guid artworkId, [FromQuery] bool fullSize = false, [FromQuery] bool thumb = false, [FromQuery] bool jpgWithBg = false)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (collectionId == Guid.Empty || artworkId == Guid.Empty)
                return NotFound();

            try
            {
                var artwork = await _projectCollectionArtworkRepository.GetByIdAsync(collectionId, artworkId);
                if (artwork == null || artwork.ItemId != itemId)
                    return NotFound();

                var project = await _projectRepository.GetByIdAsync(artwork.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                // When opacity is enabled, serve PNG (transparent) by default, or JPG with background if requested
                if (artwork.Opacity)
                {
                    if (jpgWithBg)
                    {
                        byte[]? bgBytes;
                        if (thumb)
                            bgBytes = await _imageService.GetProjectCollectionArtworkJpgWithBgThumbAsync(artwork.ProjectId, collectionId, itemId, artworkId);
                        else
                            bgBytes = await _imageService.GetProjectCollectionArtworkJpgWithBgAsync(artwork.ProjectId, collectionId, itemId, artworkId);
                        if (bgBytes == null || bgBytes.Length == 0)
                            return NotFound();
                        return File(bgBytes, "image/jpeg");
                    }
                    else
                    {
                        byte[]? pngBytes;
                        if (thumb)
                            pngBytes = await _imageService.GetProjectCollectionArtworkPngThumbAsync(artwork.ProjectId, collectionId, itemId, artworkId);
                        else if (fullSize)
                        {
                            pngBytes = await _imageService.GetProjectCollectionArtworkFullSizePngAsync(artwork.ProjectId, collectionId, itemId, artworkId);
                            // Fall back to regular PNG if full-size doesn't exist
                            if (pngBytes == null || pngBytes.Length == 0)
                                pngBytes = await _imageService.GetProjectCollectionArtworkPngAsync(artwork.ProjectId, collectionId, itemId, artworkId);
                        }
                        else
                            pngBytes = await _imageService.GetProjectCollectionArtworkPngAsync(artwork.ProjectId, collectionId, itemId, artworkId);
                        if (pngBytes == null || pngBytes.Length == 0)
                            return NotFound();
                        return File(pngBytes, "image/png");
                    }
                }

                byte[]? bytes;
                if (thumb)
                    bytes = await _imageService.GetProjectCollectionArtworkThumbAsync(artwork.ProjectId, collectionId, itemId, artworkId);
                else if (fullSize)
                {
                    bytes = await _imageService.GetProjectCollectionArtworkFullSizeAsync(artwork.ProjectId, collectionId, itemId, artworkId);
                    // Fall back to regular image if full-size doesn't exist
                    if (bytes == null || bytes.Length == 0)
                        bytes = await _imageService.GetProjectCollectionArtworkImageAsync(artwork.ProjectId, collectionId, itemId, artworkId);
                }
                else
                    bytes = await _imageService.GetProjectCollectionArtworkImageAsync(artwork.ProjectId, collectionId, itemId, artworkId);
                if (bytes == null || bytes.Length == 0)
                    return NotFound();

                return File(bytes, "image/jpeg");
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("generate-artwork-thumbnail")]
        public async Task<IActionResult> GenerateArtworkThumbnail([FromBody] GenerateArtworkThumbnailRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.CollectionId == Guid.Empty || request.ItemId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Collection ID and Item ID are required." });

            try
            {
                var artwork = await _projectCollectionArtworkRepository.GetByCollectionAndItemIdAsync(request.CollectionId, request.ItemId);
                if (artwork == null)
                    return Json(new ApiResponse { success = false, message = "Artwork not found." });

                var project = await _projectRepository.GetByIdAsync(artwork.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                var generated = artwork.Opacity
                    ? await _imageService.GenerateProjectCollectionArtworkPngThumbAsync(artwork.ProjectId, request.CollectionId, request.ItemId, artwork.Id)
                    : await _imageService.GenerateProjectCollectionArtworkThumbAsync(artwork.ProjectId, request.CollectionId, request.ItemId, artwork.Id);
                return Json(new ApiResponse { success = generated });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("estimate-collection-tokens")]
        public async Task<IActionResult> EstimateCollectionTokens([FromBody] EstimateCollectionTokensRequest request)
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

                var items = (await _projectItemRepository.GetByProjectIdAsync(request.ProjectId)).ToList();
                var artworkList = await _projectItemArtworkRepository.GetByProjectIdAsync(request.ProjectId);
                var customItemIds = artworkList.Where(a => a.ArtworkType == "custom").Select(a => a.ItemId).ToHashSet();
                var aiItems = items.Where(i => !customItemIds.Contains(i.Id)).OrderBy(i => i.Index).ToList();

                var blueprints = await _projectBlueprintRepository.GetByProjectIdAsync(request.ProjectId);

                var generations = new List<CollectionArtworkGenerationDto>();
                var seen = new HashSet<string>();

                foreach (var bp in blueprints)
                {
                    if (string.IsNullOrWhiteSpace(bp.PlacementJson))
                        continue;

                    try
                    {
                        var placementsList = JsonSerializer.Deserialize<List<PlacementDto>>(bp.PlacementJson);
                        if (placementsList == null) continue;

                        foreach (var placement in placementsList)
                        {
                            var itemId = placement.GetItemId();
                            if (itemId == Guid.Empty) continue;

                            var aiItem = aiItems.FirstOrDefault(i => i.Id == itemId);
                            if (aiItem == null) continue;

                            var (pw, ph) = placement.GetDimensions();
                            var resolution = ImageGenerationForOpenAI.FindBestResolution($"{pw}x{ph}");
                            var parts = resolution.Split('x');
                            var w = int.Parse(parts[0]);
                            var h = int.Parse(parts[1]);

                            var key = $"{itemId}_{w}_{h}";
                            if (seen.Contains(key)) continue;
                            seen.Add(key);

                            generations.Add(new CollectionArtworkGenerationDto
                            {
                                ItemId = itemId,
                                Width = w,
                                Height = h
                            });
                        }
                    }
                    catch { continue; }
                }

                var totalTokens = generations.Count * 2;

                var socialMediaItems = aiItems.Where(i => i.SocialMedia && !seen.Any(s => s.StartsWith($"{i.Id}_")));
                foreach (var item in socialMediaItems)
                {
                    generations.Add(new CollectionArtworkGenerationDto
                    {
                        ItemId = item.Id,
                        Width = 1024,
                        Height = 1024
                    });
                    totalTokens += 2;
                }

                var itemIndexMap = aiItems.ToDictionary(i => i.Id, i => i.Index);
                generations = generations.OrderBy(g => itemIndexMap.TryGetValue(g.ItemId, out var idx) ? idx : int.MaxValue).ToList();

                return Json(new ApiResponse
                {
                    success = true,
                    data = new EstimateCollectionTokensResponse
                    {
                        Generations = generations,
                        TotalTokens = totalTokens,
                        ArtworkCount = generations.Count
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("get-product-image-variants")]
        public async Task<IActionResult> GetProductImageVariants([FromQuery] Guid projectId, [FromQuery] Guid collectionId)
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

                var blueprints = (await _projectBlueprintRepository.GetByProjectIdAsync(projectId)).ToList();
                var collectionArtworkList = (await _projectCollectionArtworkRepository.GetByCollectionIdAsync(collectionId)).ToList();

                var printifyBlueprintIds = blueprints.Select(b => b.BlueprintId).Where(id => id > 0).Distinct().ToList();
                var allVariants = printifyBlueprintIds.Count > 0
                    ? (await _variantRepository.GetByBlueprintIdsAsync(printifyBlueprintIds)).ToList()
                    : new List<PrintifyBlueprintVariant>();
                var allImages = printifyBlueprintIds.Count > 0
                    ? (await _printifyBlueprintImageRepository.GetByBlueprintIdsAsync(printifyBlueprintIds)).ToList()
                    : new List<PrintifyBlueprintImage>();
                var allImageVariants = allImages.Count > 0
                    ? (await _printifyBlueprintImageVariantRepository.GetByBlueprintImageIdsAsync(allImages.Select(img => img.Id))).ToList()
                    : new List<PrintifyBlueprintImageVariant>();
                var imageVariantsByImageId = allImageVariants.GroupBy(v => v.BlueprintImageId)
                    .ToDictionary(g => g.Key, g => g.Select(v => v.VariantColor).ToList());

                var variantsByBlueprint = allVariants.GroupBy(v => v.BlueprintId).ToDictionary(g => g.Key, g => g.ToList());
                var imagesByBlueprint = allImages.GroupBy(i => i.BlueprintId).ToDictionary(g => g.Key, g => g.ToList());

                var existingProductImages = collectionId != Guid.Empty
                    ? (await _projectCollectionProductImageRepository.GetByCollectionIdAsync(collectionId)).ToList()
                    : new List<ProjectCollectionProductImage>();

                var printifyBlueprintsById = printifyBlueprintIds.Count > 0
                    ? (await _printifyBlueprintRepository.GetByBlueprintIdsAsync(printifyBlueprintIds)).ToDictionary(b => b.BlueprintId)
                    : new Dictionary<int, PrintifyBlueprint>();

                var genModel = await _imageGenerationModelRepository.GetByModelKeyAsync("openai");
                var tokenCost = _tokenCostOptions.Cost > 0 ? _tokenCostOptions.Cost : 0.01m;

                IImageTokens? tokenizer = null;
                if (genModel != null)
                {
                    var imageGen = _imageGenerations.FirstOrDefault(g => g.ModelKey.Equals("openai", StringComparison.OrdinalIgnoreCase));
                    if (imageGen != null)
                        tokenizer = imageGen.CreateTokenizer(genModel);
                }

                var result = new List<object>();

                foreach (var bp in blueprints)
                {
                    var printifyBlueprintId = bp.BlueprintId;

                    var selectedVariantIds = new HashSet<int>();
                    if (!string.IsNullOrWhiteSpace(bp.BlueprintJson))
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(bp.BlueprintJson);
                            if (doc.RootElement.TryGetProperty("variantIds", out var vIdsEl) && vIdsEl.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var v in vIdsEl.EnumerateArray())
                                {
                                    if (v.TryGetInt32(out var vId))
                                        selectedVariantIds.Add(vId);
                                }
                            }
                        }
                        catch { }
                    }

                    var selectedVariantColors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (printifyBlueprintId > 0 && variantsByBlueprint.TryGetValue(printifyBlueprintId, out var bpVariants))
                    {
                        foreach (var v in bpVariants)
                        {
                            if (selectedVariantIds.Count > 0 && !selectedVariantIds.Contains(v.VariantId))
                                continue;

                            selectedVariantColors.Add(v.Color);
                        }
                    }

                    var placements = new List<object>();
                    var placementKeys = new List<string>();
                    if (!string.IsNullOrWhiteSpace(bp.PlacementJson))
                    {
                        try
                        {
                            var placementArr = System.Text.Json.JsonSerializer.Deserialize<List<PlacementDto>>(bp.PlacementJson);
                            if (placementArr != null)
                            {
                                int placementIndex = 0;
                                foreach (var placement in placementArr)
                                {
                                    var placementName = placement.Position ?? "";
                                    placementKeys.Add(placementName);

                                    var source = placement.Source ?? "";
                                    var itemId = placement.ItemId?.ToString() ?? "";

                                    Guid? artworkId = null;
                                    string artworkUrl = null;

                                    if (source == "item" && !string.IsNullOrWhiteSpace(itemId))
                                    {
                                        var artwork = collectionArtworkList.FirstOrDefault(a => a.ItemId.ToString() == itemId);
                                        if (artwork != null && artwork.Active)
                                        {
                                            artworkId = artwork.Id;
                                            artworkUrl = $"/api/projects/collection/{collectionId}/item/{itemId}/artwork/{artwork.Id}";
                                        }
                                    }

                                    placements.Add(new
                                    {
                                        placementIndex,
                                        placement = placementName,
                                        placementName,
                                        itemId = itemId ?? "",
                                        artworkId,
                                        artworkUrl
                                    });
                                    placementIndex++;
                                }
                            }
                        }
                        catch { }
                    }

                    string imagePrompt = "";
                    if (printifyBlueprintId > 0 && printifyBlueprintsById.TryGetValue(printifyBlueprintId, out var printifyBp))
                        imagePrompt = printifyBp.ImagePrompt ?? "";

                    var existingProductImage = existingProductImages.FirstOrDefault(pi => pi.ProjectBlueprintId == bp.Id);
                    var prompt = existingProductImage?.Prompt ?? "";

                    var combinedPrompt = $"{imagePrompt}\n{prompt}".Trim();

                    var variants = new List<object>();
                    if (printifyBlueprintId > 0)
                    {
                        var printifyProductsForVariants = await _printifyProductRepository.GetByCollectionIdAsync(collectionId);
                        var collectionProductsForVariants = await _productRepository.GetByCollectionIdAsync(collectionId);
                        var productByBlueprintId = collectionProductsForVariants.ToDictionary(p => p.ProjectBlueprintId);
                        var printifyProductForVariants = printifyProductsForVariants.FirstOrDefault(p => productByBlueprintId.TryGetValue(bp.Id, out var prod) && prod.Id == p.ProductId);

                        var mockupDims = new List<(int width, int height)>();
                        if (printifyProductForVariants != null)
                        {
                            var mockups = (await _mockupRepository.GetByPrintifyProductIdAsync(printifyProductForVariants.Id)).ToList();
                            var defaultMockups = mockups.Where(m => m.IsDefault).Take(2).ToList();
                            var selectedMockups = defaultMockups.Count > 0
                                ? defaultMockups
                                : mockups.Take(2).ToList();

                            foreach (var mockup in selectedMockups)
                            {
                                var imgBytes = await _imageService.GetProjectCollectionMockupAsync(
                                    projectId, collectionId, mockup.Id);
                                var dims = await _imageService.GetImageDimensionsAsync(imgBytes);
                                mockupDims.Add(dims ?? (1024, 1024));
                            }
                        }

                        foreach (var color in selectedVariantColors)
                        {
                            var combos = new List<object>();
                            foreach (var p in placements)
                            {
                                dynamic placement = p;
                                int inputImageCount = mockupDims.Count + 1;

                                var inputImages = new List<(int width, int height)>(mockupDims);

                                var artworkId = (Guid?)placement.artworkId;
                                var placementArtwork = artworkId != null
                                    ? collectionArtworkList.FirstOrDefault(a => a.Id == artworkId.Value)
                                    : null;
                                if (placementArtwork != null && placementArtwork.Width > 0 && placementArtwork.Height > 0)
                                    inputImages.Add((placementArtwork.Width, placementArtwork.Height));
                                else
                                    inputImages.Add((1024, 1024));

                                int comboTokens;
                                if (tokenizer != null)
                                {
                                    var tokenResult = tokenizer.CalculateTokens(combinedPrompt, 2048, 2048, "medium", inputImages, "auto", tokenCost);
                                    comboTokens = tokenResult.PlatformTokens;
                                }
                                else
                                {
                                    comboTokens = Math.Max(1, inputImageCount);
                                }

                                combos.Add(new
                                {
                                    placementIndex = placement.placementIndex,
                                    placement = placement.placement,
                                    placementName = placement.placementName,
                                    tokens = comboTokens,
                                    inputImageCount,
                                    hasArtwork = artworkId != null
                                });
                            }

                            variants.Add(new
                            {
                                variantColor = color,
                                combos
                            });
                        }
                    }

                    result.Add(new
                    {
                        projectBlueprintId = bp.Id,
                        blueprintName = bp.Name,
                        printifyBlueprintId,
                        imagePrompt,
                        prompt,
                        placements,
                        variants
                    });
                }

                return Json(new ApiResponse { success = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("estimate-product-image-tokens")]
        public async Task<IActionResult> EstimateProductImageTokens([FromBody] GenerateProductImageRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.ProjectId == Guid.Empty || request.CollectionId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Project ID and Collection ID are required." });

            try
            {
                var project = await _projectRepository.GetByIdAsync(request.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                var bp = await _projectBlueprintRepository.GetByIdAsync(request.ProjectBlueprintId);
                if (bp == null || bp.ProjectId != request.ProjectId)
                    return Json(new ApiResponse { success = false, message = "Blueprint not found." });

                var printifyBlueprintId = bp.BlueprintId;

                var printifyBlueprint = printifyBlueprintId > 0
                    ? await _printifyBlueprintRepository.GetByBlueprintIdAsync(printifyBlueprintId)
                    : null;

                var collectionArtwork = (await _projectCollectionArtworkRepository.GetByCollectionIdAsync(request.CollectionId)).ToList();

                // Collect all placement artworks with their placement names
                var placementArtworks = new List<(string PlacementName, Guid ItemId, Guid ArtworkId, byte[] ImageBytes)>();
                try
                {
                    var placementArr = System.Text.Json.JsonSerializer.Deserialize<List<PlacementDto>>(bp.PlacementJson ?? "[]");
                    if (placementArr != null)
                    {
                        foreach (var placement in placementArr)
                        {
                            var pItemId = placement.GetItemId();
                            if (pItemId == Guid.Empty) continue;

                            var pArtwork = collectionArtwork.FirstOrDefault(a => a.ItemId == pItemId && a.Active);
                            if (pArtwork == null) continue;

                            var pImgBytes = await _imageService.GetProjectCollectionArtworkImageAsync(request.ProjectId, request.CollectionId, pItemId, pArtwork.Id);
                            if (pImgBytes == null || pImgBytes.Length == 0) continue;

                            placementArtworks.Add((placement.Position ?? "", pItemId, pArtwork.Id, pImgBytes));
                        }
                    }
                }
                catch { }

                if (placementArtworks.Count == 0)
                    return Json(new ApiResponse { success = false, message = "No accepted artwork found for any placement." });

                var promptBuilder = new StringBuilder();
                promptBuilder.AppendLine("Apply the following artwork designs onto the product shown in the reference image.");
                promptBuilder.AppendLine("Place the product in a realistic, appealing scenario as described below.");
                if (!string.IsNullOrWhiteSpace(printifyBlueprint?.ImagePrompt))
                {
                    promptBuilder.AppendLine();
                    promptBuilder.AppendLine($"Product context: {printifyBlueprint.ImagePrompt}");
                }
                if (!string.IsNullOrWhiteSpace(request.Prompt))
                {
                    promptBuilder.AppendLine();
                    promptBuilder.AppendLine($"User prompt: {request.Prompt}");
                }
                if (!string.IsNullOrWhiteSpace(request.RequestedChanges))
                {
                    promptBuilder.AppendLine();
                    promptBuilder.AppendLine($"Requested Changes: {request.RequestedChanges}");
                }

                var finalPrompt = promptBuilder.ToString().Trim();

                if (request.ModelId <= 0)
                    return Json(new ApiResponse { success = false, message = "Model ID is required." });

                var genModel = await _imageGenerationModelRepository.GetByIdAsync(request.ModelId);
                if (genModel == null)
                    return Json(new ApiResponse { success = false, message = "Image model not found in database." });

                var tokenCost = _tokenCostOptions.Cost > 0 ? _tokenCostOptions.Cost : 0.01m;

                IImageTokens? tokenizer = null;
                var imageGen = _imageGenerations.FirstOrDefault(g => g.ModelKey.Equals(genModel.ModelKey, StringComparison.OrdinalIgnoreCase));
                if (imageGen != null)
                    tokenizer = imageGen.CreateTokenizer(genModel);

                var inputImages = new List<(int width, int height)>();

                if (printifyBlueprintId > 0)
                {
                    var printifyProducts = await _printifyProductRepository.GetByCollectionIdAsync(request.CollectionId);
                    var collectionProducts = await _productRepository.GetByCollectionIdAsync(request.CollectionId);
                    var productByBlueprintId = collectionProducts.ToDictionary(p => p.ProjectBlueprintId);
                    var printifyProduct = printifyProducts.FirstOrDefault(p => productByBlueprintId.TryGetValue(request.ProjectBlueprintId, out var prod) && prod.Id == p.ProductId);

                    var mockupImageCount = 0;

                    if (printifyProduct != null)
                    {
                        var mockups = (await _mockupRepository.GetByPrintifyProductIdAsync(printifyProduct.Id)).ToList();
                        var selectedMockups = mockups
                            .Where(m => request.MockupImageIds != null && request.MockupImageIds.Contains(m.Id))
                            .ToList();
                        if (selectedMockups.Count == 0)
                        {
                            var defaultMockups = mockups.Where(m => m.IsDefault).Take(2).ToList();
                            selectedMockups = defaultMockups.Count > 0
                                ? defaultMockups
                                : mockups.Take(2).ToList();
                        }

                        foreach (var mockup in selectedMockups)
                        {
                            var imgBytes = await _imageService.GetProjectCollectionMockupAsync(
                                request.ProjectId, request.CollectionId, mockup.Id);
                            var dims = await _imageService.GetImageDimensionsAsync(imgBytes);
                            inputImages.Add(dims ?? (1024, 1024));
                            mockupImageCount++;
                        }
                    }

                    // Fallback: if no mockup images were found, use the Printify blueprint image for the variant color
                    if (mockupImageCount == 0 && !string.IsNullOrWhiteSpace(request.VariantColor))
                    {
                        var bpImages = (await _printifyBlueprintImageRepository.GetByBlueprintIdAsync(printifyBlueprintId)).ToList();
                        if (bpImages.Count > 0)
                        {
                            var bpImageIds = bpImages.Select(img => img.Id).ToList();
                            var variants = (await _printifyBlueprintImageVariantRepository.GetByBlueprintImageIdsAsync(bpImageIds)).ToList();
                            var matchingVariant = variants.FirstOrDefault(v =>
                                v.VariantColor.Equals(request.VariantColor, StringComparison.OrdinalIgnoreCase));
                            if (matchingVariant != null)
                            {
                                var matchingImage = bpImages.First(img => img.Id == matchingVariant.BlueprintImageId);
                                var imgBytes = await _imageService.GetPrintifyCatalogImageAsync(printifyBlueprintId, matchingImage.ImageIndex, false);
                                if (imgBytes != null && imgBytes.Length > 0)
                                {
                                    var dims = await _imageService.GetImageDimensionsAsync(imgBytes);
                                    inputImages.Add(dims ?? (1024, 1024));
                                }
                            }
                        }
                    }
                }

                foreach (var pa in placementArtworks)
                {
                    var dims = await _imageService.GetImageDimensionsAsync(pa.ImageBytes);
                    inputImages.Add(dims ?? (1024, 1024));
                }

                int textInputTokens = 0;
                int imageInputTokens = 0;
                int imageOutputTokens = 0;
                decimal estimatedCostUSD = 0m;
                int totalTokens = 0;

                if (tokenizer != null)
                {
                    var tokenResult = tokenizer.CalculateTokens(finalPrompt, 2048, 2048, "medium", inputImages, "auto", tokenCost);
                    textInputTokens = tokenResult.TextInputTokens;
                    imageInputTokens = tokenResult.ImageInputTokens;
                    imageOutputTokens = tokenResult.ImageOutputTokens;
                    estimatedCostUSD = tokenResult.EstimatedCostUSD;
                    totalTokens = tokenResult.PlatformTokens;
                }
                else
                {
                    totalTokens = Math.Max(1, inputImages.Count);
                }

                return Json(new ApiResponse
                {
                    success = true,
                    data = totalTokens
                });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        private static string GetPositionLabel(int position) => position switch
        {
            1 => "front",
            2 => "back",
            3 => "top",
            4 => "bottom",
            5 => "left",
            6 => "right",
            _ => "front"
        };

        [HttpPost("generate-product-image")]
        public async Task<IActionResult> GenerateProductImage([FromBody] GenerateProductImageRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.ProjectId == Guid.Empty || request.CollectionId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Project ID and Collection ID are required." });

            try
            {
                var project = await _projectRepository.GetByIdAsync(request.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                var bp = await _projectBlueprintRepository.GetByIdAsync(request.ProjectBlueprintId);
                if (bp == null || bp.ProjectId != request.ProjectId)
                    return Json(new ApiResponse { success = false, message = "Blueprint not found." });

                var printifyBlueprintId = bp.BlueprintId;

                var printifyBlueprint = printifyBlueprintId > 0
                    ? await _printifyBlueprintRepository.GetByBlueprintIdAsync(printifyBlueprintId)
                    : null;

                var collectionArtwork = (await _projectCollectionArtworkRepository.GetByCollectionIdAsync(request.CollectionId)).ToList();

                // Collect all placement artworks with their placement names
                var placementArtworks = new List<(string PlacementName, Guid ItemId, Guid ArtworkId, byte[] ImageBytes)>();
                try
                {
                    var placementArr = System.Text.Json.JsonSerializer.Deserialize<List<PlacementDto>>(bp.PlacementJson ?? "[]");
                    if (placementArr != null)
                    {
                        foreach (var placement in placementArr)
                        {
                            var pItemId = placement.GetItemId();
                            if (pItemId == Guid.Empty) continue;

                            var pArtwork = collectionArtwork.FirstOrDefault(a => a.ItemId == pItemId && a.Active);
                            if (pArtwork == null) continue;

                            var pImgBytes = await _imageService.GetProjectCollectionArtworkImageAsync(request.ProjectId, request.CollectionId, pItemId, pArtwork.Id);
                            if (pImgBytes == null || pImgBytes.Length == 0) continue;

                            placementArtworks.Add((placement.Position ?? "", pItemId, pArtwork.Id, pImgBytes));
                        }
                    }
                }
                catch { }

                if (placementArtworks.Count == 0)
                    return Json(new ApiResponse { success = false, message = "No accepted artwork found for any placement." });

                var promptBuilder = new StringBuilder();
                promptBuilder.AppendLine("Apply the following artwork designs onto the product shown in the reference image.");
                promptBuilder.AppendLine("Place the product in a realistic, appealing scenario as described below.");

                if (request.ModelId <= 0)
                    return Json(new ApiResponse { success = false, message = "Model ID is required." });

                var genModel = await _imageGenerationModelRepository.GetByIdAsync(request.ModelId);
                if (genModel == null)
                    return Json(new ApiResponse { success = false, message = "Image model not found in database." });

                var inputImages = new List<byte[]>();
                var inputImageRefs = new List<object>();

                if (printifyBlueprintId > 0)
                {
                    var printifyProducts = await _printifyProductRepository.GetByCollectionIdAsync(request.CollectionId);
                    var collectionProducts = await _productRepository.GetByCollectionIdAsync(request.CollectionId);
                    var productByBlueprintId = collectionProducts.ToDictionary(p => p.ProjectBlueprintId);
                    var printifyProduct = printifyProducts.FirstOrDefault(p => productByBlueprintId.TryGetValue(request.ProjectBlueprintId, out var prod) && prod.Id == p.ProductId);

                    var printifyImageCount = 0;
                    var printifyImagePositions = new List<string>();

                    if (printifyProduct != null)
                    {
                        var mockups = (await _mockupRepository.GetByPrintifyProductIdAsync(printifyProduct.Id)).ToList();
                        var selectedMockups = mockups
                            .Where(m => request.MockupImageIds != null && request.MockupImageIds.Contains(m.Id))
                            .ToList();
                        if (selectedMockups.Count == 0)
                        {
                            var defaultMockups = mockups.Where(m => m.IsDefault).Take(2).ToList();
                            selectedMockups = defaultMockups.Count > 0
                                ? defaultMockups
                                : mockups.Take(2).ToList();
                        }

                        foreach (var mockup in selectedMockups)
                        {
                            var imgBytes = await _imageService.GetProjectCollectionMockupAsync(
                                request.ProjectId, request.CollectionId, mockup.Id);
                            if (imgBytes != null && imgBytes.Length > 0)
                            {
                                inputImages.Add(imgBytes);
                                inputImageRefs.Add(new { type = "mockup", id = mockup.Id.ToString() });
                                printifyImageCount++;
                                printifyImagePositions.Add(mockup.Position ?? "front");
                            }
                        }
                    }

                    // Fallback: if no mockup images were found, use the ProjectBlueprintProductImages reference image
                    if (printifyImageCount == 0 && !string.IsNullOrWhiteSpace(request.VariantColor))
                    {
                        var bpImages = (await _printifyBlueprintImageRepository.GetByBlueprintIdAsync(printifyBlueprintId)).ToList();
                        if (bpImages.Count > 0)
                        {
                            var blueprintProductImages = (await _projectBlueprintProductImageRepository.GetByProjectBlueprintIdAsync(request.ProjectBlueprintId)).ToList();
                            var selected = blueprintProductImages
                                .FirstOrDefault(pbi =>
                                    pbi.VariantColor.Equals(request.VariantColor, StringComparison.OrdinalIgnoreCase) &&
                                    pbi.ImageId.HasValue);
                            if (selected == null)
                            {
                                selected = blueprintProductImages
                                    .FirstOrDefault(pbi => pbi.ImageId.HasValue);
                            }
                            if (selected != null)
                            {
                                var matchingImage = bpImages.FirstOrDefault(img => img.Id == selected.ImageId.GetValueOrDefault());
                                if (matchingImage != null)
                                {
                                    var imgBytes = await _imageService.GetPrintifyCatalogImageAsync(printifyBlueprintId, matchingImage.ImageIndex, false);
                                    if (imgBytes != null && imgBytes.Length > 0)
                                    {
                                        inputImages.Add(imgBytes);
                                        inputImageRefs.Add(new { type = "blueprint", id = matchingImage.Id.ToString() });
                                        printifyImageCount++;
                                        printifyImagePositions.Add(GetPositionLabel(matchingImage.Position));
                                    }
                                }
                            }
                        }
                    }

                    if (printifyImageCount > 0)
                    {
                        promptBuilder.AppendLine();
                        for (var i = 0; i < printifyImageCount; i++)
                        {
                            var posStr = printifyImagePositions[i];
                            promptBuilder.AppendLine($"Image {i + 1} is the mockup product image for {bp.Name} ({posStr} view). Isolate the product from the person and background in this reference image to use in the final output.");
                        }

                        promptBuilder.AppendLine();
                        promptBuilder.AppendLine("The artwork designs shown on the product in the reference mockup images must remain in the exact same position and at the exact same size as they appear in those mockups. Do not move, resize, reposition, or alter the artwork placement in any way.");
                    }
                }

                // Add artwork images after printify images, then describe placements
                var artworkStartIndex = inputImages.Count + 1;
                for (var i = 0; i < placementArtworks.Count; i++)
                {
                    inputImages.Add(placementArtworks[i].ImageBytes);
                    inputImageRefs.Add(new { type = "artwork", id = placementArtworks[i].ArtworkId.ToString() });
                    promptBuilder.AppendLine($"Image {artworkStartIndex + i} should be placed on the {placementArtworks[i].PlacementName} of the product.");
                }

                if (!string.IsNullOrWhiteSpace(printifyBlueprint?.ImagePrompt))
                {
                    promptBuilder.AppendLine();
                    promptBuilder.AppendLine($"Product context: {printifyBlueprint.ImagePrompt}");
                }
                if (!string.IsNullOrWhiteSpace(request.Prompt))
                {
                    promptBuilder.AppendLine();
                    promptBuilder.AppendLine($"User prompt: {request.Prompt}");
                }
                if (!string.IsNullOrWhiteSpace(request.RequestedChanges))
                {
                    promptBuilder.AppendLine();
                    promptBuilder.AppendLine($"Requested Changes: {request.RequestedChanges}");
                }

                var finalPrompt = promptBuilder.ToString().Trim();

                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                };

                var imageGen = _imageGenerations.FirstOrDefault(g => g.ModelKey.Equals(genModel.ModelKey, StringComparison.OrdinalIgnoreCase));
                if (imageGen == null)
                    return Json(new ApiResponse { success = false, message = "Image generation service not available." });

                var existing = await _projectCollectionProductImageRepository.GetByCollectionBlueprintProductImageIdAsync(
                    request.CollectionId, request.ProjectBlueprintId, request.ProductImageId);

                string? previousResponseId = null;
                if (existing != null && !string.IsNullOrWhiteSpace(existing.ResponseId) && !string.IsNullOrWhiteSpace(request.RequestedChanges))
                {
                    previousResponseId = existing.ResponseId;
                }

                var genRequest = new ImageGenerationRequest
                {
                    Model = genModel.Model,
                    Prompt = finalPrompt,
                    InputImages = inputImages,
                    Width = 2048,
                    Height = 2048,
                    Quality = "medium",
                    PreviousResponseId = previousResponseId,
                    UseResponsesApi = false
                };

                var productImageInputDimensions = new List<(int width, int height)>();
                foreach (var img in inputImages)
                {
                    var dims = await _imageService.GetImageDimensionsAsync(img);
                    if (dims.HasValue)
                        productImageInputDimensions.Add(dims.Value);
                }

                var productTokenCost = _tokenCostOptions.Cost > 0 ? _tokenCostOptions.Cost : 0.01m;
                var productTokenizer = imageGen.CreateTokenizer(genModel);
                var productTokenCalc = productTokenizer.CalculateTokens(finalPrompt, 2048, 2048, "medium", productImageInputDimensions, "auto", productTokenCost);
                var productTokensToUse = productTokenCalc.PlatformTokens;

                if (!await _aiTokenService.UseTokensAsync(userId, productTokensToUse))
                    return Json(new ApiResponse { success = false, message = "Not enough tokens to generate the product image. Please purchase more tokens before continuing." });

                var genResult = await imageGen.GenerateAsync(genRequest);

                var productImage = new ProjectCollectionProductImage
                {
                    ProjectId = request.ProjectId,
                    CollectionId = request.CollectionId,
                    ProjectBlueprintId = request.ProjectBlueprintId,
                    ProductImageId = request.ProductImageId,
                    ImageModel = genModel.Model,
                    Prompt = request.Prompt,
                    Width = 2048,
                    Height = 2048,
                    Accepted = false,
                    ResponseId = genResult.ResponseId ?? "",
                    VariantColor = request.VariantColor ?? ""
                };

                if (existing != null)
                {
                    productImage.Id = existing.Id;
                    productImage.ResponseId = genResult.ResponseId ?? existing.ResponseId;
                    await _projectCollectionProductImageRepository.UpdateAsync(productImage);
                }
                else
                {
                    productImage = await _projectCollectionProductImageRepository.CreateAsync(productImage);
                }

                await _imageService.SaveProjectCollectionProductImageAsync(request.ProjectId, request.CollectionId, productImage.Id, genResult.ImageBytes);

                // Update product name if provided
                if (!string.IsNullOrWhiteSpace(request.ProductName))
                {
                    var collectionProduct = await _productRepository.GetByCollectionAndBlueprintIdAsync(request.CollectionId, request.ProjectBlueprintId);
                    if (collectionProduct != null && collectionProduct.Name != request.ProductName)
                    {
                        collectionProduct.Name = request.ProductName;
                        await _productRepository.UpdateAsync(collectionProduct);
                    }
                }

                await _projectImageGenerationRepository.CreateAsync(new ProjectImageGeneration
                {
                    ProjectId = request.ProjectId,
                    CollectionId = request.CollectionId,
                    BlueprintId = request.ProjectBlueprintId,
                    AppUserId = userId,
                    ImageGenerationId = genModel.Id,
                    InputTextTokens = genResult.InputTokens,
                    InputImageTokens = 0,
                    OutputTokens = genResult.OutputTokens,
                    Tokens = productTokenCalc.PlatformTokens,
                    Prompt = finalPrompt,
                    Filename = $"{productImage.Id}.jpg",
                    Resolution = "2048x2048",
                    InputImages = inputImages.Count,
                    InputImageJson = System.Text.Json.JsonSerializer.Serialize(inputImageRefs),
                    Type = 2,
                    Cost = (int)Math.Round(productTokenCalc.EstimatedCostUSD * 100)
                });

                return Json(new ApiResponse
                {
                    success = true,
                    data = new
                    {
                        id = productImage.Id,
                        projectBlueprintId = productImage.ProjectBlueprintId,
                        productImageId = productImage.ProductImageId,
                        imageUrl = $"/api/projects/collection/{request.CollectionId}/product-image/{productImage.Id}?thumb=true",
                        accepted = productImage.Accepted
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("accept-product-image")]
        public async Task<IActionResult> AcceptProductImage([FromBody] AcceptProductImageRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            try
            {
                var productImage = await _projectCollectionProductImageRepository.GetByIdAsync(request.ProductImageId);
                if (productImage == null || productImage.CollectionId != request.CollectionId)
                    return Json(new ApiResponse { success = false, message = "Product image not found." });

                productImage.Accepted = true;
                await _projectCollectionProductImageRepository.UpdateAsync(productImage);

                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("collection/{collectionId}/product-images")]
        public async Task<IActionResult> GetProductImages(Guid collectionId)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            try
            {
                var images = await _projectCollectionProductImageRepository.GetByCollectionIdAsync(collectionId);
                return Json(new ApiResponse
                {
                    success = true,
                    data = images.Select(img => new
                    {
                        id = img.Id,
                        projectBlueprintId = img.ProjectBlueprintId,
                        productImageId = img.ProductImageId,
                        accepted = img.Accepted,
                        active = img.Active,
                        prompt = img.Prompt,
                        imageUrl = $"/api/projects/collection/{collectionId}/product-image/{img.Id}?thumb=true"
                    })
                });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("collection/{collectionId}/product-image/{productImageId}")]
        public async Task<IActionResult> GetProductImageFile(Guid collectionId, Guid productImageId, [FromQuery] bool thumb = false)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            try
            {
                var productImage = await _projectCollectionProductImageRepository.GetByIdAsync(productImageId);
                if (productImage == null || productImage.CollectionId != collectionId)
                    return NotFound();

                var project = await _projectRepository.GetByIdAsync(productImage.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                byte[] bytes;
                if (thumb)
                    bytes = await _imageService.GetProjectCollectionProductImageThumbAsync(productImage.ProjectId, collectionId, productImageId);
                else
                    bytes = await _imageService.GetProjectCollectionProductImageAsync(productImage.ProjectId, collectionId, productImageId);

                if (bytes == null || bytes.Length == 0)
                    return NotFound();

                return File(bytes, "image/jpeg");
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        private static readonly string[] PlacementNames = [
            "Front", "Back", "Left Sleeve", "Right Sleeve", "Left", "Right",
            "Top", "Bottom", "Inside", "Outside"
        ];

        private static string GetPlacementName(int num) =>
            num >= 0 && num < PlacementNames.Length ? PlacementNames[num] : $"Placement {num + 1}";

        [HttpGet("get-product-blueprint-images")]
        public async Task<IActionResult> GetProductBlueprintImages([FromQuery] Guid projectBlueprintId)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            try
            {
                var images = await _projectBlueprintProductImageRepository.GetByProjectBlueprintIdAsync(projectBlueprintId);
                return Json(new ApiResponse
                {
                    success = true,
                    data = images.Select(img => new
                    {
                        id = img.Id,
                        projectBlueprintId = img.ProjectBlueprintId,
                        title = img.Title,
                        variantColor = img.VariantColor,
                        status = img.Status,
                        prompt = img.Prompt,
                        imageId = img.ImageId
                    })
                });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("get-all-product-blueprint-images")]
        public async Task<IActionResult> GetAllProductBlueprintImages([FromQuery] Guid projectId)
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

                var blueprints = await _projectBlueprintRepository.GetByProjectIdAsync(projectId);
                var blueprintIds = blueprints.Select(b => b.Id).ToList();
                var blueprintNameMap = blueprints.ToDictionary(b => b.Id, b => b.Name);
                var images = await _projectBlueprintProductImageRepository.GetByBlueprintIdsAsync(blueprintIds);

                return Json(new ApiResponse
                {
                    success = true,
                    data = images.Select(img => new
                    {
                        id = img.Id,
                        projectBlueprintId = img.ProjectBlueprintId,
                        blueprintName = blueprintNameMap.TryGetValue(img.ProjectBlueprintId, out var name) ? name : "",
                        title = img.Title,
                        variantColor = img.VariantColor,
                        status = img.Status,
                        prompt = img.Prompt,
                        imageId = img.ImageId
                    })
                });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("create-product-blueprint-image")]
        public async Task<IActionResult> CreateProductBlueprintImage([FromBody] CreateProductBlueprintImageRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            try
            {
                var bp = await _projectBlueprintRepository.GetByIdAsync(request.ProjectBlueprintId);
                if (bp == null || bp.ProjectId != request.ProjectId)
                    return Json(new ApiResponse { success = false, message = "Blueprint not found." });

                var image = await _projectBlueprintProductImageRepository.CreateAsync(new ProjectBlueprintProductImage
                {
                    ProjectId = request.ProjectId,
                    ProjectBlueprintId = request.ProjectBlueprintId,
                    Title = request.Title,
                    VariantColor = request.VariantColor,
                    Status = 1,
                    Prompt = request.Prompt,
                    ImageId = request.ImageId
                });

                return Json(new ApiResponse
                {
                    success = true,
                    data = new
                    {
                        id = image.Id,
                        projectBlueprintId = image.ProjectBlueprintId,
                        title = image.Title,
                        variantColor = image.VariantColor,
                        status = image.Status,
                        prompt = image.Prompt,
                        imageId = image.ImageId
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("update-product-blueprint-image")]
        public async Task<IActionResult> UpdateProductBlueprintImage([FromBody] UpdateProductBlueprintImageRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            try
            {
                var image = await _projectBlueprintProductImageRepository.GetByIdAsync(request.Id);
                if (image == null)
                    return Json(new ApiResponse { success = false, message = "Product image not found." });

                image.Title = request.Title;
                image.VariantColor = request.VariantColor;
                image.Prompt = request.Prompt;
                image.ImageId = request.ImageId;
                await _projectBlueprintProductImageRepository.UpdateAsync(image);

                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("delete-product-blueprint-image")]
        public async Task<IActionResult> DeleteProductBlueprintImage([FromBody] DeleteProductBlueprintImageRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            try
            {
                var image = await _projectBlueprintProductImageRepository.GetByIdAsync(request.Id);
                if (image == null)
                    return Json(new ApiResponse { success = false, message = "Product image not found." });

                await _projectBlueprintProductImageRepository.SetStatusAsync(request.Id, 0);

                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("update-collection-products-active")]
        public async Task<IActionResult> UpdateCollectionProductsActive([FromBody] UpdateCollectionProductsActiveRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.CollectionId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Collection ID is required." });

            try
            {
                var collection = await _projectCollectionRepository.GetByIdAsync(request.CollectionId);
                if (collection == null)
                    return Json(new ApiResponse { success = false, message = "Collection not found." });

                var products = request.Products.Select(p => new ProjectCollectionProduct
                {
                    CollectionId = request.CollectionId,
                    ProjectBlueprintId = p.ProjectBlueprintId,
                    Active = p.Active
                });

                await _productRepository.BulkUpdateActiveAsync(request.CollectionId, products);
                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("update-collection-product-name")]
        public async Task<IActionResult> UpdateCollectionProductName([FromBody] UpdateCollectionProductNameRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.CollectionId == Guid.Empty || request.ProjectBlueprintId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Collection ID and Project Blueprint ID are required." });

            try
            {
                var product = await _productRepository.GetByCollectionAndBlueprintIdAsync(request.CollectionId, request.ProjectBlueprintId);
                if (product == null)
                    return Json(new ApiResponse { success = false, message = "Product not found." });

                product.Name = request.Name ?? "";
                await _productRepository.UpdateAsync(product);
                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("get-collection-products")]
        public async Task<IActionResult> GetCollectionProducts([FromQuery] Guid collectionId)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (collectionId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Collection ID is required." });

            try
            {
                var products = await _productRepository.GetByCollectionIdAsync(collectionId);
                return Json(new ApiResponse
                {
                    success = true,
                    data = products.Select(p => new
                    {
                        id = p.Id,
                        projectId = p.ProjectId,
                        collectionId = p.CollectionId,
                        projectBlueprintId = p.ProjectBlueprintId,
                        blueprintId = p.BlueprintId,
                        name = p.Name,
                        active = p.Active
                    })
                });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

    }
}
