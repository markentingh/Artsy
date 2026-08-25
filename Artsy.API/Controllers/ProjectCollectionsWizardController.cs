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
                var collection = await _projectCollectionRepository.GetByIdAsync(collectionId);
                if (collection == null || collection.Status != 1)
                    return Json(new ApiResponse { success = false, message = "Collection not found." });

                var artwork = await _projectCollectionArtworkRepository.GetByCollectionIdAsync(collectionId);
                var result = new List<object>();
                foreach (var a in artwork)
                {
                    var placements = await _projectCollectionArtworkPlacementRepository.GetByArtworkIdAsync(a.Id);
                    var placementList = placements.ToList();

                    // Check if this artwork needs regeneration by comparing stored TotalPlacements with a fresh plan
                    bool needsRegeneration = false;
                    try
                    {
                        var plan = await _artworkGenerationPlanService.BuildPlanAsync(collection.ProjectId, collectionId, a.ItemId, resolutionTier: 2);
                        needsRegeneration = a.TotalPlacements != plan.TotalPlacements;
                    }
                    catch { /* if plan fails, assume no regeneration needed */ }

                    // Build group placements from placement records that have a GroupId
                    var groupPlacements = placementList
                        .Where(p => p.GroupId.HasValue)
                        .GroupBy(p => p.GroupId!.Value)
                        .Select(g => (object)new
                        {
                            groupId = g.Key,
                            placements = g.OrderBy(p => p.Index).Select(p => new
                            {
                                position = p.Position,
                                index = p.Index,
                                width = p.Width,
                                height = p.Height,
                                fullSize = p.FullSize,
                                printifyImageId = p.PrintifyImageId
                            }).ToList()
                        }).ToList();

                    result.Add(new
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
                        opacity = a.Opacity,
                        totalPlacements = a.TotalPlacements,
                        needsRegeneration,
                        hasGroups = groupPlacements.Count > 0,
                        groupPlacements,
                        placements = placementList.Select(p => new
                        {
                            id = p.Id,
                            width = p.Width,
                            height = p.Height,
                            index = p.Index,
                            fullSize = p.FullSize,
                            printifyImageId = p.PrintifyImageId,
                            groupId = p.GroupId,
                            position = p.Position
                        })
                    });
                }
                return Json(new ApiResponse { success = true, data = result });
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

                if (request.ModelId == null || request.ModelId <= 0)
                    return Json(new ApiResponse { success = false, message = "Image model is required." });

                var genModel = await _imageGenerationModelRepository.GetByIdAsync(request.ModelId.Value);
                if (genModel == null)
                    return Json(new ApiResponse { success = false, message = "Image model not found in database." });

                // Build the generation plan (placements, variants, references, prompt, mask requirements)
                var plan = await _artworkGenerationPlanService.BuildPlanAsync(
                    request.ProjectId, request.CollectionId, request.ItemId,
                    request.RequestedChanges, request.Answers, resolutionTier: 2);

                if (string.IsNullOrWhiteSpace(plan.FinalPrompt))
                    return Json(new ApiResponse { success = false, message = "Prompt is required to generate artwork." });

                var opacitySettings = _opacityService.ParseOpacityJson(plan.Artwork.OpacityJson);

                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                };

                var modelRequest = new OpenAIImageRequest();
                modelRequest.Model = genModel.Model;
                modelRequest.Prompt = plan.FinalPrompt;
                modelRequest.Size = $"{plan.Width}x{plan.Height}";
                modelRequest.Quality = "medium";

                var inputImages = plan.ReferenceImages.Select(r => r.ImageBytes).ToList();
                var inputImageRefs = plan.ReferenceImages.Select(r => (object)new { type = r.Type, id = r.Id }).ToList();

                var imageModelJson = JsonSerializer.Serialize(modelRequest, jsonOptions);

                var collectionArtwork = new ProjectCollectionArtwork
                {
                    ProjectId = request.ProjectId,
                    CollectionId = request.CollectionId,
                    ItemId = request.ItemId,
                    Active = true,
                    Width = plan.Width,
                    Height = plan.Height,
                    ImageModel = genModel.Model,
                    Prompt = plan.FinalPrompt,
                    Index = item.Index,
                    TotalPlacements = plan.TotalPlacements
                };
                var created = await _projectCollectionArtworkRepository.UpsertAsync(collectionArtwork);

                // Delete any existing placement variants for this artwork only on first generation (regeneration scenario)
                var isFirstGeneration = request.GenerationIndex == null || request.GenerationIndex <= 0;
                if (isFirstGeneration)
                    await _projectCollectionArtworkPlacementRepository.DeleteByArtworkIdAsync(created.Id);

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
                    var tokenCost = _tokenCostOptions.Cost > 0 ? _tokenCostOptions.Cost : 0.01m;
                    var tokenizer = imageGen.CreateTokenizer(genModel);

                    var inputImageDimensions = new List<(int width, int height)>();
                    foreach (var img in inputImages)
                    {
                        var dims = await _imageService.GetImageDimensionsAsync(img);
                        if (dims.HasValue)
                            inputImageDimensions.Add(dims.Value);
                    }

                    // Determine which task(s) to generate: single task if generationIndex is provided, otherwise all
                    var taskIndices = request.GenerationIndex.HasValue
                        ? new List<int> { request.GenerationIndex.Value }
                        : Enumerable.Range(0, plan.Tasks.Count).ToList();

                    foreach (var i in taskIndices)
                    {
                        if (i < 0 || i >= plan.Tasks.Count) continue;
                        var task = plan.Tasks[i];
                        var customSize = $"{task.Width}x{task.Height}";

                        var genRequest = new ImageGenerationRequest
                        {
                            Model = genModel.Model,
                            Prompt = plan.FinalPrompt,
                            InputImages = inputImages,
                            CustomSize = plan.TotalPlacements > 0 ? customSize : null,
                            Width = task.Width,
                            Height = task.Height,
                            Quality = genQuality,
                            PreviousResponseId = i == 0 ? previousResponseId : null,
                            UseResponsesApi = false
                        };

                        // Token calculation per task
                        var tokenCalc = tokenizer.CalculateTokens(plan.FinalPrompt, task.Width, task.Height, genQuality, inputImageDimensions, "auto", tokenCost);
                        if (!await _aiTokenService.UseTokensAsync(userId, tokenCalc.PlatformTokens))
                            throw new InvalidOperationException("Not enough tokens to generate the artwork. Please purchase more tokens before continuing.");

                        var genResult = await imageGen.GenerateAsync(genRequest);

                        // Crop the generated image to the placement's actual aspect ratio
                        // (only needed when the ratio exceeds 3:1 and was clamped for generation)
                        byte[] finalImageBytes = genResult.ImageBytes;
                        if (task.NeedsCrop)
                        {
                            finalImageBytes = await _imageService.CropToPlacementAsync(genResult.ImageBytes, task.PlacementWidth, task.PlacementHeight, task.CropX, task.CropY);
                        }

                        // --- Seamless placement group task ---
                        // Generate one tall image, then cut it up into individual placement images
                        if (task.GroupId.HasValue)
                        {
                            var groupId = task.GroupId.Value;

                            // Save the full image as the main artwork image (for preview/display)
                            await SaveArtworkImageAsync(request.ProjectId, request.CollectionId, request.ItemId, created.Id, finalImageBytes, request.IsFullSize, opacitySettings);

                            // Get the image bytes to cut up (use PNG if opacity, otherwise JPG)
                            byte[] imageToCut = finalImageBytes;
                            if (opacitySettings != null && opacitySettings.ChromaKeys.Count > 0)
                            {
                                // Apply chroma key to get PNG with transparency, then cut that up
                                var pngBytes = await _opacityService.ApplyChromaKeysAsync(finalImageBytes, opacitySettings);
                                if (opacitySettings.Overlay != null)
                                    pngBytes = await _opacityService.ApplyOverlayAsync(pngBytes, opacitySettings.Overlay.Color);
                                // Save the full PNG
                                if (request.IsFullSize)
                                    await _imageService.SaveProjectCollectionArtworkFullSizePngAsync(request.ProjectId, request.CollectionId, request.ItemId, created.Id, pngBytes);
                                else
                                    await _imageService.SaveProjectCollectionArtworkPngAsync(request.ProjectId, request.CollectionId, request.ItemId, created.Id, pngBytes);
                                imageToCut = pngBytes;
                            }

                            // Cut the image vertically into segments based on group placement heights
                            var segmentHeights = task.GroupPlacements.Select(p => p.Height).ToList();
                            var segments = await _imageService.CutImageVerticalAsync(imageToCut, segmentHeights);

                            // Save each segment to the groups folder
                            for (var segIdx = 0; segIdx < segments.Count && segIdx < task.GroupPlacements.Count; segIdx++)
                            {
                                var placement = task.GroupPlacements[segIdx];
                                var segBytes = segments[segIdx];

                                // Apply flips: FlipX = top/bottom mirror, FlipY = left/right mirror
                                if (placement.FlipX)
                                    segBytes = await _imageService.MirrorXAsync(segBytes);
                                if (placement.FlipY)
                                    segBytes = await _imageService.MirrorYAsync(segBytes);

                                // Save as JPG (or PNG if opacity)
                                if (opacitySettings != null && opacitySettings.ChromaKeys.Count > 0)
                                {
                                    if (request.IsFullSize)
                                        await _imageService.SaveProjectCollectionArtworkGroupImageFullSizePngAsync(request.ProjectId, request.CollectionId, request.ItemId, created.Id, groupId, placement.Position, segBytes);
                                    else
                                        await _imageService.SaveProjectCollectionArtworkGroupImagePngAsync(request.ProjectId, request.CollectionId, request.ItemId, created.Id, groupId, placement.Position, segBytes);
                                }
                                else
                                {
                                    if (request.IsFullSize)
                                        await _imageService.SaveProjectCollectionArtworkGroupImageFullSizeAsync(request.ProjectId, request.CollectionId, request.ItemId, created.Id, groupId, placement.Position, segBytes);
                                    else
                                        await _imageService.SaveProjectCollectionArtworkGroupImageAsync(request.ProjectId, request.CollectionId, request.ItemId, created.Id, groupId, placement.Position, segBytes);
                                }

                                // Create placement variant record with group info
                                var placementVariant = new ProjectCollectionArtworkPlacement
                                {
                                    CollectionArtworkId = created.Id,
                                    Width = placement.Width,
                                    Height = placement.Height,
                                    Index = segIdx,
                                    FullSize = request.IsFullSize,
                                    ResponseId = genResult.ResponseId ?? "",
                                    GroupId = groupId,
                                    Position = placement.Position
                                };
                                await _projectCollectionArtworkPlacementRepository.CreateAsync(placementVariant);
                            }

                            // Track the first variant's response ID on the artwork
                            created.ResponseId = genResult.ResponseId ?? "";
                            created.Opacity = opacitySettings != null && opacitySettings.ChromaKeys.Count > 0;

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
                                Prompt = plan.FinalPrompt,
                                Filename = request.IsFullSize ? $"{created.Id}_fullsize.jpg" : $"{created.Id}.jpg",
                                Resolution = customSize,
                                InputImages = inputImages.Count,
                                InputImageJson = System.Text.Json.JsonSerializer.Serialize(inputImageRefs),
                                Type = 1,
                                Cost = (int)Math.Round(tokenCalc.EstimatedCostUSD * 100)
                            });
                        }
                        else if (plan.TotalPlacements == 0)
                        {
                            // Single artwork (no placements) — save as the main artwork image
                            await SaveArtworkImageAsync(request.ProjectId, request.CollectionId, request.ItemId, created.Id, finalImageBytes, request.IsFullSize, opacitySettings);

                            created.Active = true;
                            created.ResponseId = genResult.ResponseId ?? "";
                            created.Opacity = opacitySettings != null && opacitySettings.ChromaKeys.Count > 0;
                            if (request.IsFullSize)
                                created.FullSize = true;
                            await _projectCollectionArtworkRepository.UpdateAsync(created);
                            await _projectCollectionArtworkRepository.SetPrintifyImageIdAsync(created.Id, "");

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
                                Prompt = plan.FinalPrompt,
                                Filename = request.IsFullSize ? $"{created.Id}_fullsize.jpg" : $"{created.Id}.jpg",
                                Resolution = $"{task.Width}x{task.Height}",
                                InputImages = inputImages.Count,
                                InputImageJson = System.Text.Json.JsonSerializer.Serialize(inputImageRefs),
                                Type = 1,
                                Cost = (int)Math.Round(tokenCalc.EstimatedCostUSD * 100)
                            });
                        }
                        else
                        {
                            // Variant artwork — save as a placement variant
                            await SaveArtworkPlacementImageAsync(request.ProjectId, request.CollectionId, request.ItemId, created.Id, i, finalImageBytes, request.IsFullSize, opacitySettings);

                            // Create the placement variant record
                            var placementVariant = new ProjectCollectionArtworkPlacement
                            {
                                CollectionArtworkId = created.Id,
                                Width = task.PlacementWidth,
                                Height = task.PlacementHeight,
                                Index = i,
                                FullSize = request.IsFullSize,
                                ResponseId = genResult.ResponseId ?? ""
                            };
                            await _projectCollectionArtworkPlacementRepository.CreateAsync(placementVariant);

                            // Track the first variant's response ID on the artwork for "make changes" flow
                            if (i == 0)
                            {
                                created.ResponseId = genResult.ResponseId ?? "";
                                created.Opacity = opacitySettings != null && opacitySettings.ChromaKeys.Count > 0;
                            }

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
                                Prompt = plan.FinalPrompt,
                                Filename = request.IsFullSize ? $"{created.Id}_{i}_fullsize.jpg" : $"{created.Id}_{i}.jpg",
                                Resolution = customSize,
                                InputImages = inputImages.Count,
                                InputImageJson = System.Text.Json.JsonSerializer.Serialize(inputImageRefs),
                                Type = 1,
                                Cost = (int)Math.Round(tokenCalc.EstimatedCostUSD * 100)
                            });
                        }
                    }

                    // Only update artwork and generate thumbnail on the last generation
                    var isLastGeneration = request.GenerationIndex == null || request.GenerationIndex.Value >= plan.Tasks.Count - 1;
                    if (isLastGeneration)
                    {
                        if (request.IsFullSize)
                            created.FullSize = true;
                        await _projectCollectionArtworkRepository.UpdateAsync(created);
                        await _projectCollectionArtworkRepository.SetPrintifyImageIdAsync(created.Id, "");

                        // Generate thumbnail after all segments are flipped and saved
                        if (created.Opacity)
                            await _imageService.GenerateProjectCollectionArtworkPngThumbAsync(request.ProjectId, request.CollectionId, request.ItemId, created.Id);
                        else
                            await _imageService.GenerateProjectCollectionArtworkThumbAsync(request.ProjectId, request.CollectionId, request.ItemId, created.Id);
                    }
                    else
                    {
                        // Update artwork record mid-generation (to persist response ID etc.)
                        await _projectCollectionArtworkRepository.UpdateAsync(created);
                    }
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

        async Task SaveArtworkImageAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId, byte[] imageBytes, bool isFullSize, OpacitySettings? opacitySettings)
        {
            if (isFullSize)
                await _imageService.SaveProjectCollectionArtworkFullSizeAsync(projectId, collectionId, itemId, artworkId, imageBytes);
            else
                await _imageService.SaveProjectCollectionArtworkAsync(projectId, collectionId, itemId, artworkId, imageBytes);

            if (opacitySettings != null && opacitySettings.ChromaKeys.Count > 0)
            {
                var pngBytes = await _opacityService.ApplyChromaKeysAsync(imageBytes, opacitySettings);
                await _imageService.SaveProjectCollectionArtworkChromaAsync(projectId, collectionId, itemId, artworkId, pngBytes);

                if (opacitySettings.Overlay != null && !string.IsNullOrWhiteSpace(opacitySettings.Overlay.Color))
                    pngBytes = await _opacityService.ApplyOverlayAsync(pngBytes, opacitySettings.Overlay.Color);

                if (isFullSize)
                    await _imageService.SaveProjectCollectionArtworkFullSizePngAsync(projectId, collectionId, itemId, artworkId, pngBytes);
                else
                    await _imageService.SaveProjectCollectionArtworkPngAsync(projectId, collectionId, itemId, artworkId, pngBytes);

                var (bgBytes, bgColor) = await GetBackgroundBytesAsync(projectId, collectionId, opacitySettings);
                var jpgWithBgBytes = await _opacityService.CompositeOverBackgroundAsync(pngBytes, bgBytes, bgColor);
                await _imageService.SaveProjectCollectionArtworkJpgWithBgAsync(projectId, collectionId, itemId, artworkId, jpgWithBgBytes);
            }
        }

        async Task SaveArtworkPlacementImageAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId, int placementIndex, byte[] imageBytes, bool isFullSize, OpacitySettings? opacitySettings)
        {
            if (isFullSize)
                await _imageService.SaveProjectCollectionArtworkPlacementFullSizeAsync(projectId, collectionId, itemId, artworkId, placementIndex, imageBytes);
            else
                await _imageService.SaveProjectCollectionArtworkPlacementAsync(projectId, collectionId, itemId, artworkId, placementIndex, imageBytes);

            if (opacitySettings != null && opacitySettings.ChromaKeys.Count > 0)
            {
                var pngBytes = await _opacityService.ApplyChromaKeysAsync(imageBytes, opacitySettings);
                if (opacitySettings.Overlay != null && !string.IsNullOrWhiteSpace(opacitySettings.Overlay.Color))
                    pngBytes = await _opacityService.ApplyOverlayAsync(pngBytes, opacitySettings.Overlay.Color);

                if (isFullSize)
                    await _imageService.SaveProjectCollectionArtworkPlacementFullSizePngAsync(projectId, collectionId, itemId, artworkId, placementIndex, pngBytes);
                else
                    await _imageService.SaveProjectCollectionArtworkPlacementPngAsync(projectId, collectionId, itemId, artworkId, placementIndex, pngBytes);

                var (bgBytes, bgColor) = await GetBackgroundBytesAsync(projectId, collectionId, opacitySettings);
                var jpgWithBgBytes = await _opacityService.CompositeOverBackgroundAsync(pngBytes, bgBytes, bgColor);
                await _imageService.SaveProjectCollectionArtworkPlacementJpgWithBgAsync(projectId, collectionId, itemId, artworkId, placementIndex, jpgWithBgBytes);
            }
        }

        async Task<(byte[]? Bytes, string? Color)> GetBackgroundBytesAsync(Guid projectId, Guid collectionId, OpacitySettings opacitySettings)
        {
            if (opacitySettings.Background == null)
                return (null, null);

            byte[]? bgBytes = null;
            string? bgColor = null;

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
                        var bgCollectionArtwork = await _projectCollectionArtworkRepository.GetByCollectionAndItemIdAsync(collectionId, bgId);
                        if (bgCollectionArtwork != null)
                        {
                            bgBytes = await _imageService.GetProjectCollectionArtworkImageAsync(projectId, collectionId, bgId, bgCollectionArtwork.Id);
                            if (bgBytes == null || bgBytes.Length == 0)
                                bgBytes = await _imageService.GetProjectCollectionArtworkFullSizeAsync(projectId, collectionId, bgId, bgCollectionArtwork.Id);
                        }
                    }
                }
                catch { }
            }

            if (bgBytes == null && !string.IsNullOrWhiteSpace(opacitySettings.Background.Color))
                bgColor = opacitySettings.Background.Color;

            return (bgBytes, bgColor);
        }

        [HttpPost("fix-seamless-placements")]
        public async Task<IActionResult> FixSeamlessPlacements([FromBody] FixSeamlessPlacementsRequest request)
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
                    return Json(new ApiResponse { success = false, message = "No artwork found." });

                // Get placement records for this artwork
                var placements = await _projectCollectionArtworkPlacementRepository.GetByArtworkIdAsync(artwork.Id);
                var placementList = placements.ToList();

                // Only group placements need fixing
                var groupPlacements = placementList.Where(p => p.GroupId.HasValue).ToList();
                if (groupPlacements.Count == 0)
                    return Json(new ApiResponse { success = false, message = "No seamless placement groups found for this artwork." });

                // Parse opacity settings
                OpacitySettings? opacitySettings = null;
                if (artwork.Opacity)
                {
                    var itemArtworkList = await _projectItemArtworkRepository.GetByItemIdAsync(request.ItemId);
                    var itemArtwork = itemArtworkList.FirstOrDefault();
                    opacitySettings = _opacityService.ParseOpacityJson(itemArtwork?.OpacityJson);
                }

                // Load the already-generated full image
                byte[] fullImageBytes;
                if (artwork.Opacity && opacitySettings != null && opacitySettings.ChromaKeys.Count > 0)
                {
                    fullImageBytes = await _imageService.GetProjectCollectionArtworkFullSizePngAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id);
                    if (fullImageBytes == null || fullImageBytes.Length == 0)
                        fullImageBytes = await _imageService.GetProjectCollectionArtworkPngAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id);
                }
                else
                {
                    fullImageBytes = await _imageService.GetProjectCollectionArtworkFullSizeAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id);
                    if (fullImageBytes == null || fullImageBytes.Length == 0)
                        fullImageBytes = await _imageService.GetProjectCollectionArtworkImageAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id);
                }

                if (fullImageBytes == null || fullImageBytes.Length == 0)
                    return Json(new ApiResponse { success = false, message = "Generated artwork image not found." });

                // Build a lookup of flip flags from the blueprint placement group images
                var flipLookup = new Dictionary<(Guid GroupId, string Position), (bool FlipX, bool FlipY)>();
                var blueprints = await _projectBlueprintRepository.GetByProjectIdAsync(request.ProjectId);
                foreach (var bp in blueprints)
                {
                    var bpGroups = await _placementGroupRepository.GetByProjectAndBlueprintAsync(request.ProjectId, bp.BlueprintId);
                    foreach (var bg in bpGroups)
                    {
                        var bgImages = await _placementGroupImageRepository.GetByGroupIdAsync(bg.Id);
                        foreach (var img in bgImages)
                        {
                            if (img.ArtworkId == request.ItemId && !string.IsNullOrWhiteSpace(img.Position))
                                flipLookup[(bg.Id, img.Position)] = (img.FlipX, img.FlipY);
                        }
                    }
                }

                // Group placements by GroupId and re-cut
                var groups = groupPlacements.GroupBy(p => p.GroupId!.Value);
                foreach (var group in groups)
                {
                    var groupId = group.Key;
                    var orderedPlacements = group.OrderBy(p => p.Index).ToList();

                    // Cut the image vertically into segments based on placement heights
                    var segmentHeights = orderedPlacements.Select(p => p.Height).ToList();
                    var segments = await _imageService.CutImageVerticalAsync(fullImageBytes, segmentHeights);

                    for (var segIdx = 0; segIdx < segments.Count && segIdx < orderedPlacements.Count; segIdx++)
                    {
                        var placement = orderedPlacements[segIdx];
                        var segBytes = segments[segIdx];
                        var position = placement.Position ?? "";

                        // Apply flips: FlipX = top/bottom mirror, FlipY = left/right mirror
                        if (flipLookup.TryGetValue((groupId, position), out var flips))
                        {
                            if (flips.FlipX)
                                segBytes = await _imageService.MirrorXAsync(segBytes);
                            if (flips.FlipY)
                                segBytes = await _imageService.MirrorYAsync(segBytes);
                        }

                        // Save the segment
                        if (opacitySettings != null && opacitySettings.ChromaKeys.Count > 0)
                        {
                            if (artwork.FullSize)
                                await _imageService.SaveProjectCollectionArtworkGroupImageFullSizePngAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, groupId, position, segBytes);
                            else
                                await _imageService.SaveProjectCollectionArtworkGroupImagePngAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, groupId, position, segBytes);
                        }
                        else
                        {
                            if (artwork.FullSize)
                                await _imageService.SaveProjectCollectionArtworkGroupImageFullSizeAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, groupId, position, segBytes);
                            else
                                await _imageService.SaveProjectCollectionArtworkGroupImageAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, groupId, position, segBytes);
                        }
                    }
                }

                // Reset full-size flags and Printify image IDs so the upscale step detects this artwork needs upscaling again
                // and the Printify upload step re-uploads the corrected images
                foreach (var p in placementList)
                {
                    await _projectCollectionArtworkPlacementRepository.SetFullSizeAsync(p.Id, false);
                    await _projectCollectionArtworkPlacementRepository.SetPrintifyImageIdAsync(p.Id, "");
                }
                artwork.FullSize = false;
                await _projectCollectionArtworkRepository.SetPrintifyImageIdAsync(artwork.Id, "");
                await _projectCollectionArtworkRepository.UpdateAsync(artwork);

                // Regenerate thumbnail after segments are re-cut and flipped
                if (artwork.Opacity)
                    await _imageService.GenerateProjectCollectionArtworkPngThumbAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id);
                else
                    await _imageService.GenerateProjectCollectionArtworkThumbAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id);

                return Json(new ApiResponse { success = true });
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

                if (artwork.FullSize && !request.Force)
                    return Json(new ApiResponse { success = true, data = artwork });

                // When forcing re-upscale, reset placement full-size flags and Printify image IDs
                if (request.Force)
                {
                    var existingPlacements = await _projectCollectionArtworkPlacementRepository.GetByArtworkIdAsync(artwork.Id);
                    foreach (var p in existingPlacements)
                    {
                        await _projectCollectionArtworkPlacementRepository.SetFullSizeAsync(p.Id, false);
                        await _projectCollectionArtworkPlacementRepository.SetPrintifyImageIdAsync(p.Id, "");
                    }
                    artwork.FullSize = false;
                    await _projectCollectionArtworkRepository.SetPrintifyImageIdAsync(artwork.Id, "");
                }

                // Get placement variants for this artwork
                var placements = await _projectCollectionArtworkPlacementRepository.GetByArtworkIdAsync(artwork.Id);
                var placementList = placements.ToList();

                // Determine token cost: 2 tokens per variant (or 2 for single artwork)
                var variantCount = Math.Max(1, placementList.Count);
                if (!await _aiTokenService.UseTokensAsync(userId, 2 * variantCount))
                    return Json(new ApiResponse { success = false, message = "Not enough tokens to generate the artwork. Please purchase more tokens before continuing." });

                // Parse opacity settings for re-applying chroma key after upscale
                OpacitySettings? opacitySettings = null;
                if (artwork.Opacity)
                {
                    var itemArtworkList = await _projectItemArtworkRepository.GetByItemIdAsync(request.ItemId);
                    var itemArtwork = itemArtworkList.FirstOrDefault();
                    opacitySettings = _opacityService.ParseOpacityJson(itemArtwork?.OpacityJson) ?? new OpacitySettings();
                }

                if (placementList.Count > 0)
                {
                    // Separate group placements from standard placements
                    var groupPlacements = placementList.Where(p => p.GroupId.HasValue).ToList();
                    var standardPlacements = placementList.Where(p => !p.GroupId.HasValue).ToList();

                    // Build a lookup of flip flags from the blueprint placement group images
                    var flipLookup = new Dictionary<(Guid GroupId, string Position), (bool FlipX, bool FlipY)>();
                    var blueprints = await _projectBlueprintRepository.GetByProjectIdAsync(request.ProjectId);
                    foreach (var bp in blueprints)
                    {
                        var bpGroups = await _placementGroupRepository.GetByProjectAndBlueprintAsync(request.ProjectId, bp.BlueprintId);
                        foreach (var bg in bpGroups)
                        {
                            var bgImages = await _placementGroupImageRepository.GetByGroupIdAsync(bg.Id);
                            foreach (var img in bgImages)
                            {
                                if (img.ArtworkId == request.ItemId && !string.IsNullOrWhiteSpace(img.Position))
                                    flipLookup[(bg.Id, img.Position)] = (img.FlipX, img.FlipY);
                            }
                        }
                    }

                    // Upscale group placement images: all placements in a group share the max width
                    var groups = groupPlacements.GroupBy(p => p.GroupId!.Value);
                    foreach (var group in groups)
                    {
                        var groupId = group.Key;
                        var orderedGroupPlacements = group.OrderBy(p => p.Index).ToList();
                        var maxWidth = orderedGroupPlacements.Max(p => p.Width);
                        var totalHeight = orderedGroupPlacements.Sum(p => p.Height);

                        // Load the full combined group image (before it was cut into segments)
                        byte[] fullImageBytes;
                        if (artwork.Opacity)
                        {
                            fullImageBytes = await _imageService.GetProjectCollectionArtworkFullSizePngAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id);
                            if (fullImageBytes == null || fullImageBytes.Length == 0)
                                fullImageBytes = await _imageService.GetProjectCollectionArtworkPngAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id);
                        }
                        else
                        {
                            fullImageBytes = await _imageService.GetProjectCollectionArtworkFullSizeAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id);
                            if (fullImageBytes == null || fullImageBytes.Length == 0)
                                fullImageBytes = await _imageService.GetProjectCollectionArtworkImageAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id);
                        }

                        if (fullImageBytes == null || fullImageBytes.Length == 0)
                            continue;

                        // Determine scale based on max width vs current image width
                        var fullDims = await _imageService.GetImageDimensionsAsync(fullImageBytes);
                        var fullWidth = fullDims?.width ?? maxWidth;
                        var scale = maxWidth > fullWidth * 4 ? 4 : 2;

                        // Upscale the full combined image once
                        var upscaledBytes = await _imageUpscaler.UpscaleAsync(fullImageBytes, scale);

                        // Downsample/resize to maxWidth, maintaining aspect ratio
                        var upscaledDims = await _imageService.GetImageDimensionsAsync(upscaledBytes);
                        if (upscaledDims.HasValue && upscaledDims.Value.width != maxWidth)
                        {
                            if (artwork.Opacity)
                                upscaledBytes = await _imageService.ResizeToWidthPngAsync(upscaledBytes, maxWidth);
                            else
                                upscaledBytes = await _imageService.ResizeToWidthAsync(upscaledBytes, maxWidth);
                        }

                        // Cut the resized image into segments, center-cropping each to the placement's width
                        var segmentHeights = orderedGroupPlacements.Select(p => p.Height).ToList();
                        var segmentWidths = orderedGroupPlacements.Select(p => p.Width).ToList();
                        List<byte[]> segments;
                        if (artwork.Opacity)
                            segments = await _imageService.CutImageVerticalWithCenterCropPngAsync(upscaledBytes, segmentHeights, segmentWidths);
                        else
                            segments = await _imageService.CutImageVerticalWithCenterCropAsync(upscaledBytes, segmentHeights, segmentWidths);

                        // Save each segment
                        for (var segIdx = 0; segIdx < segments.Count && segIdx < orderedGroupPlacements.Count; segIdx++)
                        {
                            var placement = orderedGroupPlacements[segIdx];
                            var position = placement.Position ?? "";
                            var segBytes = segments[segIdx];

                            // Apply flips: FlipX = top/bottom mirror, FlipY = left/right mirror
                            if (flipLookup.TryGetValue((groupId, position), out var flips))
                            {
                                if (flips.FlipX)
                                    segBytes = await _imageService.MirrorXAsync(segBytes);
                                if (flips.FlipY)
                                    segBytes = await _imageService.MirrorYAsync(segBytes);
                            }

                            if (artwork.Opacity && opacitySettings != null)
                            {
                                var pngBytes = await _opacityService.ApplyChromaKeysAsync(segBytes, opacitySettings);
                                if (!string.IsNullOrWhiteSpace(opacitySettings.Overlay?.Color))
                                    pngBytes = await _opacityService.ApplyOverlayAsync(pngBytes, opacitySettings.Overlay.Color);
                                await _imageService.SaveProjectCollectionArtworkGroupImageFullSizePngAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, groupId, position, pngBytes);
                            }
                            else
                            {
                                await _imageService.SaveProjectCollectionArtworkGroupImageFullSizeAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, groupId, position, segBytes);
                            }

                            await _projectCollectionArtworkPlacementRepository.SetFullSizeAsync(placement.Id, true);

                            await _projectImageUpscaleRepository.CreateAsync(new ProjectImageUpscale
                            {
                                ProjectId = request.ProjectId,
                                CollectionId = request.CollectionId,
                                ItemId = request.ItemId,
                                ArtworkId = artwork.Id,
                                Width = placement.Width,
                                Height = placement.Height,
                                Scale = scale,
                                Created = DateTime.UtcNow
                            });
                        }
                    }

                    // Upscale standard placement variants
                    foreach (var placement in standardPlacements)
                    {
                        byte[] previewBytes;
                        if (artwork.Opacity)
                            previewBytes = await _imageService.GetProjectCollectionArtworkPlacementPngAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, placement.Index);
                        else
                            previewBytes = await _imageService.GetProjectCollectionArtworkPlacementImageAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, placement.Index);

                        if (previewBytes == null || previewBytes.Length == 0)
                            continue;

                        var maxPlacementDim = Math.Max(placement.Width, placement.Height);
                        var scale = maxPlacementDim > 4096 ? 4 : 2;

                        var upscaledBytes = await _imageUpscaler.UpscaleAsync(previewBytes, scale);

                        // Resize + center crop to exact placement dimensions
                        var targetW = placement.Width;
                        var targetH = placement.Height;
                        var stdUpscaledDims = await _imageService.GetImageDimensionsAsync(upscaledBytes);
                        if (stdUpscaledDims.HasValue && (stdUpscaledDims.Value.width != targetW || stdUpscaledDims.Value.height != targetH))
                        {
                            var (uw, uh) = stdUpscaledDims.Value;
                            var widthRatio = (double)targetW / uw;
                            var heightRatio = (double)targetH / uh;
                            var coverRatio = Math.Max(widthRatio, heightRatio);
                            var resizeW = (int)Math.Round(uw * coverRatio);
                            var resizeH = (int)Math.Round(uh * coverRatio);
                            if (artwork.Opacity)
                                upscaledBytes = await _imageService.ResizeAndCenterCropPngAsync(upscaledBytes, resizeW, resizeH, targetW, targetH);
                            else
                                upscaledBytes = await _imageService.ResizeAndCenterCropAsync(upscaledBytes, resizeW, resizeH, targetW, targetH);
                        }

                        if (artwork.Opacity && opacitySettings != null)
                        {
                            var pngBytes = await _opacityService.ApplyChromaKeysAsync(upscaledBytes, opacitySettings);
                            if (!string.IsNullOrWhiteSpace(opacitySettings.Overlay?.Color))
                                pngBytes = await _opacityService.ApplyOverlayAsync(pngBytes, opacitySettings.Overlay.Color);
                            await _imageService.SaveProjectCollectionArtworkPlacementFullSizePngAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, placement.Index, pngBytes);
                        }
                        else
                        {
                            await _imageService.SaveProjectCollectionArtworkPlacementFullSizeAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, placement.Index, upscaledBytes);
                        }

                        await _projectCollectionArtworkPlacementRepository.SetFullSizeAsync(placement.Id, true);

                        await _projectImageUpscaleRepository.CreateAsync(new ProjectImageUpscale
                        {
                            ProjectId = request.ProjectId,
                            CollectionId = request.CollectionId,
                            ItemId = request.ItemId,
                            ArtworkId = artwork.Id,
                            Width = placement.Width,
                            Height = placement.Height,
                            Scale = scale,
                            Created = DateTime.UtcNow
                        });
                    }
                }
                else
                {
                    // No placement variants — upscale the single artwork (backward compatible)
                    byte[] previewBytes;
                    if (artwork.Opacity)
                        previewBytes = await _imageService.GetProjectCollectionArtworkPngAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id);
                    else
                        previewBytes = await _imageService.GetProjectCollectionArtworkImageAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id);

                    if (previewBytes == null || previewBytes.Length == 0)
                        return Json(new ApiResponse { success = false, message = "Preview image data not found." });

                    var upscaledBytes = await _imageUpscaler.UpscaleAsync(previewBytes);

                    if (artwork.Opacity && opacitySettings != null)
                    {
                        var pngBytes = await _opacityService.ApplyChromaKeysAsync(upscaledBytes, opacitySettings);
                        if (!string.IsNullOrWhiteSpace(opacitySettings.Overlay?.Color))
                            pngBytes = await _opacityService.ApplyOverlayAsync(pngBytes, opacitySettings.Overlay.Color);
                        await _imageService.SaveProjectCollectionArtworkFullSizePngAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, pngBytes);
                    }
                    else
                    {
                        await _imageService.SaveProjectCollectionArtworkFullSizeAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, upscaledBytes);
                    }

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
                }

                artwork.FullSize = true;
                await _projectCollectionArtworkRepository.UpdateAsync(artwork);

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
                    Tokens = 2 * variantCount,
                    Prompt = "",
                    Filename = artwork.Opacity ? $"{artwork.Id}_fullsize.png" : $"{artwork.Id}_fullsize.jpg",
                    Resolution = $"{artwork.Width}x{artwork.Height}",
                    InputImages = 0,
                    InputImageJson = "[]",
                    Type = 3,
                    Cost = (int)Math.Round(2 * variantCount * (_tokenCostOptions.Cost > 0 ? _tokenCostOptions.Cost : 0.01m) * 100)
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
        public async Task<IActionResult> GetCollectionArtworkImage(Guid collectionId, Guid itemId, Guid artworkId, [FromQuery] bool fullSize = false, [FromQuery] bool thumb = false, [FromQuery] bool jpgWithBg = false, [FromQuery] int? placementIndex = null)
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

                // If a placement index is specified, serve the per-variant image
                if (placementIndex.HasValue && artwork.TotalPlacements > 0)
                {
                    var idx = placementIndex.Value;
                    byte[]? variantBytes = null;

                    // Handle jpgWithBg request for placement variants
                    if (jpgWithBg)
                    {
                        if (thumb)
                            variantBytes = await _imageService.GetProjectCollectionArtworkPlacementJpgWithBgThumbAsync(artwork.ProjectId, collectionId, itemId, artworkId, idx);
                        else
                            variantBytes = await _imageService.GetProjectCollectionArtworkPlacementJpgWithBgAsync(artwork.ProjectId, collectionId, itemId, artworkId, idx);

                        if (variantBytes != null && variantBytes.Length > 0)
                            return File(variantBytes, "image/jpeg");
                        // Fall through to base artwork bg below if not found
                    }
                    else if (artwork.Opacity)
                    {
                        if (thumb)
                        {
                            variantBytes = await _imageService.GetProjectCollectionArtworkPlacementPngAsync(artwork.ProjectId, collectionId, itemId, artworkId, idx);
                            // Fall back to generating thumb from full image
                            if (variantBytes == null || variantBytes.Length == 0)
                            {
                                var fullPng = await _imageService.GetProjectCollectionArtworkPlacementFullSizePngAsync(artwork.ProjectId, collectionId, itemId, artworkId, idx);
                                if (fullPng != null && fullPng.Length > 0)
                                    variantBytes = fullPng;
                            }
                        }
                        else if (fullSize)
                        {
                            variantBytes = await _imageService.GetProjectCollectionArtworkPlacementFullSizePngAsync(artwork.ProjectId, collectionId, itemId, artworkId, idx);
                            if (variantBytes == null || variantBytes.Length == 0)
                                variantBytes = await _imageService.GetProjectCollectionArtworkPlacementPngAsync(artwork.ProjectId, collectionId, itemId, artworkId, idx);
                        }
                        else
                        {
                            variantBytes = await _imageService.GetProjectCollectionArtworkPlacementPngAsync(artwork.ProjectId, collectionId, itemId, artworkId, idx);
                        }

                        if (variantBytes != null && variantBytes.Length > 0)
                            return File(variantBytes, "image/png");
                    }
                    else
                    {
                        if (thumb)
                            variantBytes = await _imageService.GetProjectCollectionArtworkPlacementThumbAsync(artwork.ProjectId, collectionId, itemId, artworkId, idx);
                        else if (fullSize)
                        {
                            variantBytes = await _imageService.GetProjectCollectionArtworkPlacementFullSizeAsync(artwork.ProjectId, collectionId, itemId, artworkId, idx);
                            if (variantBytes == null || variantBytes.Length == 0)
                                variantBytes = await _imageService.GetProjectCollectionArtworkPlacementImageAsync(artwork.ProjectId, collectionId, itemId, artworkId, idx);
                        }
                        else
                            variantBytes = await _imageService.GetProjectCollectionArtworkPlacementImageAsync(artwork.ProjectId, collectionId, itemId, artworkId, idx);

                        if (variantBytes != null && variantBytes.Length > 0)
                            return File(variantBytes, "image/jpeg");
                    }
                }

                // When a JPG with background is requested, try it first (regardless of opacity), then fall back
                if (jpgWithBg)
                {
                    byte[]? bgBytes;
                    if (thumb)
                        bgBytes = await _imageService.GetProjectCollectionArtworkJpgWithBgThumbAsync(artwork.ProjectId, collectionId, itemId, artworkId);
                    else
                        bgBytes = await _imageService.GetProjectCollectionArtworkJpgWithBgAsync(artwork.ProjectId, collectionId, itemId, artworkId);
                    if (bgBytes != null && bgBytes.Length > 0)
                        return File(bgBytes, "image/jpeg");
                }

                // When opacity is enabled, serve PNG (transparent) by default
                if (artwork.Opacity && !jpgWithBg)
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

        [HttpGet("collection/{collectionId}/item/{itemId}/artwork/{artworkId}/group/{groupId}/{position}")]
        public async Task<IActionResult> GetCollectionArtworkGroupImage(Guid collectionId, Guid itemId, Guid artworkId, Guid groupId, string position, [FromQuery] bool fullSize = false, [FromQuery] bool png = false)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (collectionId == Guid.Empty || artworkId == Guid.Empty || groupId == Guid.Empty)
                return NotFound();

            try
            {
                var artwork = await _projectCollectionArtworkRepository.GetByIdAsync(collectionId, artworkId);
                if (artwork == null || artwork.ItemId != itemId)
                    return NotFound();

                var project = await _projectRepository.GetByIdAsync(artwork.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                byte[]? bytes = null;
                var contentType = "image/jpeg";

                if (png || artwork.Opacity)
                {
                    contentType = "image/png";
                    if (fullSize)
                    {
                        bytes = await _imageService.GetProjectCollectionArtworkGroupImageFullSizePngAsync(artwork.ProjectId, collectionId, itemId, artworkId, groupId, position);
                        if (bytes == null || bytes.Length == 0)
                            bytes = await _imageService.GetProjectCollectionArtworkGroupImagePngAsync(artwork.ProjectId, collectionId, itemId, artworkId, groupId, position);
                    }
                    else
                        bytes = await _imageService.GetProjectCollectionArtworkGroupImagePngAsync(artwork.ProjectId, collectionId, itemId, artworkId, groupId, position);
                }
                else
                {
                    if (fullSize)
                    {
                        bytes = await _imageService.GetProjectCollectionArtworkGroupImageFullSizeAsync(artwork.ProjectId, collectionId, itemId, artworkId, groupId, position);
                        if (bytes == null || bytes.Length == 0)
                            bytes = await _imageService.GetProjectCollectionArtworkGroupImageAsync(artwork.ProjectId, collectionId, itemId, artworkId, groupId, position);
                    }
                    else
                        bytes = await _imageService.GetProjectCollectionArtworkGroupImageAsync(artwork.ProjectId, collectionId, itemId, artworkId, groupId, position);
                }

                if (bytes == null || bytes.Length == 0)
                    return NotFound();

                return File(bytes, contentType);
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

                // Filter to only items referenced by active collection products
                if (request.CollectionId.HasValue && request.CollectionId.Value != Guid.Empty)
                {
                    var collectionProducts = await _productRepository.GetByCollectionIdAsync(request.CollectionId.Value);
                    var activeBlueprintIds = collectionProducts.Where(cp => cp.Active).Select(cp => cp.ProjectBlueprintId).ToHashSet();
                    var blueprints = await _projectBlueprintRepository.GetByProjectIdAsync(request.ProjectId);
                    var activeBlueprints = blueprints.Where(bp => activeBlueprintIds.Contains(bp.Id)).ToList();

                    var activeItemIds = new HashSet<Guid>();
                    foreach (var bp in activeBlueprints)
                    {
                        if (string.IsNullOrWhiteSpace(bp.PlacementJson)) continue;
                        try
                        {
                            var placements = JsonSerializer.Deserialize<List<JsonElement>>(bp.PlacementJson);
                            if (placements == null) continue;
                            foreach (var p in placements)
                            {
                                if (p.TryGetProperty("source", out var srcEl) && srcEl.GetString() == "item" &&
                                    p.TryGetProperty("itemId", out var itemIdEl) && itemIdEl.TryGetGuid(out var iid))
                                {
                                    activeItemIds.Add(iid);
                                }
                            }
                        }
                        catch { }
                    }

                    // Also include social media items
                    var socialMediaItemIds = items.Where(i => i.SocialMedia).Select(i => i.Id).ToHashSet();
                    activeItemIds.UnionWith(socialMediaItemIds);

                    // Also include items referenced by active items via opacity backgrounds
                    var artworkByItem = artworkList.Where(a => a.OpacityJson != null).ToDictionary(a => a.ItemId);
                    var referencedByActive = new HashSet<Guid>();
                    foreach (var activeId in activeItemIds)
                    {
                        if (artworkByItem.TryGetValue(activeId, out var art) && !string.IsNullOrWhiteSpace(art.OpacityJson))
                        {
                            try
                            {
                                var opacity = JsonSerializer.Deserialize<JsonElement>(art.OpacityJson);
                                if (opacity.TryGetProperty("background", out var bgEl) &&
                                    bgEl.TryGetProperty("type", out var bgTypeEl) && bgTypeEl.GetString() == "artwork" &&
                                    bgEl.TryGetProperty("id", out var bgIdEl) && bgIdEl.TryGetGuid(out var bgId))
                                {
                                    referencedByActive.Add(bgId);
                                }
                            }
                            catch { }
                        }
                    }
                    activeItemIds.UnionWith(referencedByActive);

                    aiItems = aiItems.Where(i => activeItemIds.Contains(i.Id)).OrderBy(i => i.Index).ToList();
                }

                // Load existing collection artwork to detect placement mismatches
                var existingArtworkByItem = new Dictionary<Guid, List<Data.Entities.Projects.ProjectCollectionArtwork>>();
                if (request.CollectionId.HasValue && request.CollectionId.Value != Guid.Empty)
                {
                    var existingArtwork = await _projectCollectionArtworkRepository.GetByCollectionIdAsync(request.CollectionId.Value);
                    existingArtworkByItem = existingArtwork.GroupBy(a => a.ItemId).ToDictionary(g => g.Key, g => g.ToList());
                }

                var generations = new List<CollectionArtworkGenerationDto>();
                var totalTokens = 0m;
                var needsRegeneration = false;

                // Get tokenizer for accurate token cost estimation
                var genModel = await _imageGenerationModelRepository.GetByModelKeyAsync("openai");
                var tokenCost = _tokenCostOptions.Cost > 0 ? _tokenCostOptions.Cost : 0.01m;
                IImageTokens? tokenizer = null;
                if (genModel != null)
                {
                    var imageGen = _imageGenerations.FirstOrDefault(g => g.ModelKey.Equals("openai", StringComparison.OrdinalIgnoreCase));
                    if (imageGen != null)
                        tokenizer = imageGen.CreateTokenizer(genModel);
                }

                // Build a plan per AI item to get accurate task count, dimensions, and token cost
                foreach (var aiItem in aiItems)
                {
                    try
                    {
                        var plan = await _artworkGenerationPlanService.BuildPlanAsync(request.ProjectId, request.CollectionId ?? Guid.Empty, aiItem.Id, resolutionTier: 2);
                        var itemTokens = 0m;

                        // Check if the plan's total placements matches the stored TotalPlacements on existing artwork
                        var existingArt = existingArtworkByItem.TryGetValue(aiItem.Id, out var existingList) ? existingList.FirstOrDefault() : null;
                        var itemNeedsRegeneration = existingArt != null && existingArt.TotalPlacements != plan.TotalPlacements;

                        foreach (var task in plan.Tasks)
                        {
                            // Calculate tokens using the actual token formula (same as real generation)
                            int taskTokensInt;
                            if (tokenizer != null)
                            {
                                var tokenCalc = tokenizer.CalculateTokens(plan.FinalPrompt, task.Width, task.Height, "high", null, "auto", tokenCost);
                                taskTokensInt = tokenCalc.PlatformTokens;
                            }
                            else
                            {
                                // Fallback: rough heuristic if no model is configured
                                taskTokensInt = (int)Math.Ceiling(task.Width * task.Height / (1024.0 * 1024) * 2);
                            }

                            generations.Add(new CollectionArtworkGenerationDto
                            {
                                ItemId = aiItem.Id,
                                Width = task.Width,
                                Height = task.Height,
                                NeedsUpscale = plan.NeedsUpscale,
                                NeedsRegeneration = itemNeedsRegeneration,
                                Tokens = taskTokensInt,
                                Placements = task.Placements.Select(p => new EstimatePlacementDto
                                {
                                    BlueprintId = p.BlueprintId,
                                    BlueprintName = p.BlueprintName,
                                    Position = p.Position,
                                    Width = p.Width,
                                    Height = p.Height
                                }).ToList()
                            });

                            itemTokens += taskTokensInt;
                        }

                        if (itemNeedsRegeneration)
                            needsRegeneration = true;

                        totalTokens += itemTokens;
                    }
                    catch { continue; }
                }

                var itemIndexMap = aiItems.ToDictionary(i => i.Id, i => i.Index);
                generations = generations.OrderBy(g => itemIndexMap.TryGetValue(g.ItemId, out var idx) ? idx : int.MaxValue).ToList();

                return Json(new ApiResponse
                {
                    success = true,
                    data = new EstimateCollectionTokensResponse
                    {
                        Generations = generations,
                        TotalTokens = (int)Math.Ceiling(totalTokens),
                        ArtworkCount = generations.Count,
                        NeedsRegeneration = needsRegeneration
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
                        // Pre-load placement variants for all artworks
                        var artworkPlacementsMap = new Dictionary<Guid, List<ProjectCollectionArtworkPlacement>>();
                        foreach (var a in collectionArtwork.Where(a => a.Active))
                        {
                            if (!artworkPlacementsMap.ContainsKey(a.Id))
                                artworkPlacementsMap[a.Id] = (await _projectCollectionArtworkPlacementRepository.GetByArtworkIdAsync(a.Id)).ToList();
                        }

                        foreach (var placement in placementArr)
                        {
                            var pItemId = placement.GetItemId();
                            if (pItemId == Guid.Empty) continue;

                            var pArtwork = collectionArtwork.FirstOrDefault(a => a.ItemId == pItemId && a.Active);
                            if (pArtwork == null) continue;

                            var (pw, ph) = placement.GetDimensions();
                            var placementVariants = artworkPlacementsMap.GetValueOrDefault(pArtwork.Id, new List<ProjectCollectionArtworkPlacement>());

                            var pImgBytes = await GetPlacementSpecificArtworkAsync(
                                request.ProjectId, request.CollectionId, pItemId, pArtwork.Id, pArtwork.Opacity,
                                placement.Position ?? "", pw, ph, placementVariants);

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

                // Build detailed response with generation breakdown
                var generations = new List<CollectionArtworkGenerationDto>
                {
                    new CollectionArtworkGenerationDto
                    {
                        ItemId = Guid.Empty,
                        Width = 2048,
                        Height = 2048,
                        NeedsUpscale = true,
                        Tokens = totalTokens,
                        Placements = placementArtworks.Select(pa => new EstimatePlacementDto
                        {
                            BlueprintId = bp.BlueprintId,
                            BlueprintName = bp.Name ?? "",
                            Position = pa.PlacementName,
                            Width = 0,
                            Height = 0
                        }).ToList()
                    }
                };

                return Json(new ApiResponse
                {
                    success = true,
                    data = new
                    {
                        totalTokens,
                        generations
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Loads the correct placement-specific artwork image for a given placement position.
        /// For seamless group placements, matches by group + position.
        /// For non-grouped placements, matches by aspect ratio.
        /// Falls back to the main artwork image if no placement variant is found.
        /// </summary>
        private async Task<byte[]?> GetPlacementSpecificArtworkAsync(
            Guid projectId, Guid collectionId, Guid itemId, Guid artworkId, bool opacity,
            string position, int placementWidth, int placementHeight,
            List<ProjectCollectionArtworkPlacement> placementVariants)
        {
            if (placementVariants.Count > 0)
            {
                // Try to match by group + position first (for seamless group placements)
                var byPosition = placementVariants.FirstOrDefault(v =>
                    v.GroupId.HasValue &&
                    !string.IsNullOrWhiteSpace(v.Position) &&
                    string.Equals(v.Position, position, StringComparison.OrdinalIgnoreCase));

                if (byPosition != null)
                {
                    var img = await LoadPlacementImageAsync(projectId, collectionId, itemId, artworkId, opacity, byPosition);
                    if (img != null && img.Length > 0) return img;
                }

                // Fall back to aspect ratio matching (for non-grouped placements)
                if (placementWidth > 0 && placementHeight > 0)
                {
                    var placementRatio = (double)placementWidth / placementHeight;
                    var byRatio = placementVariants.FirstOrDefault(v =>
                    {
                        if (v.Width <= 0 || v.Height <= 0) return false;
                        var variantRatio = (double)v.Width / v.Height;
                        return Math.Abs(variantRatio - placementRatio) < 0.01;
                    });

                    if (byRatio != null)
                    {
                        var img = await LoadPlacementImageAsync(projectId, collectionId, itemId, artworkId, opacity, byRatio);
                        if (img != null && img.Length > 0) return img;
                    }
                }
            }

            // Fall back to main artwork image
            if (opacity)
            {
                var png = await _imageService.GetProjectCollectionArtworkPngAsync(projectId, collectionId, itemId, artworkId);
                if (png == null || png.Length == 0)
                    png = await _imageService.GetProjectCollectionArtworkFullSizePngAsync(projectId, collectionId, itemId, artworkId);
                return png;
            }
            else
            {
                var jpg = await _imageService.GetProjectCollectionArtworkImageAsync(projectId, collectionId, itemId, artworkId);
                if (jpg == null || jpg.Length == 0)
                    jpg = await _imageService.GetProjectCollectionArtworkFullSizeAsync(projectId, collectionId, itemId, artworkId);
                return jpg;
            }
        }

        /// <summary>
        /// Loads the fullsize (preferred) or preview image for a specific placement variant.
        /// For group placements, loads the group image. For standard placements, loads the placement image.
        /// </summary>
        private async Task<byte[]?> LoadPlacementImageAsync(
            Guid projectId, Guid collectionId, Guid itemId, Guid artworkId, bool opacity,
            ProjectCollectionArtworkPlacement placement)
        {
            // Group placement: load by group + position
            if (placement.GroupId.HasValue && !string.IsNullOrWhiteSpace(placement.Position))
            {
                var groupId = placement.GroupId.Value;
                var position = placement.Position;
                if (opacity)
                {
                    var img = await _imageService.GetProjectCollectionArtworkGroupImageFullSizePngAsync(projectId, collectionId, itemId, artworkId, groupId, position);
                    if (img == null || img.Length == 0)
                        img = await _imageService.GetProjectCollectionArtworkGroupImagePngAsync(projectId, collectionId, itemId, artworkId, groupId, position);
                    return img;
                }
                else
                {
                    var img = await _imageService.GetProjectCollectionArtworkGroupImageFullSizeAsync(projectId, collectionId, itemId, artworkId, groupId, position);
                    if (img == null || img.Length == 0)
                        img = await _imageService.GetProjectCollectionArtworkGroupImageAsync(projectId, collectionId, itemId, artworkId, groupId, position);
                    return img;
                }
            }

            // Standard placement: load by index
            var idx = placement.Index;
            if (opacity)
            {
                var img = await _imageService.GetProjectCollectionArtworkPlacementFullSizePngAsync(projectId, collectionId, itemId, artworkId, idx);
                if (img == null || img.Length == 0)
                    img = await _imageService.GetProjectCollectionArtworkPlacementPngAsync(projectId, collectionId, itemId, artworkId, idx);
                return img;
            }
            else
            {
                var img = await _imageService.GetProjectCollectionArtworkPlacementFullSizeAsync(projectId, collectionId, itemId, artworkId, idx);
                if (img == null || img.Length == 0)
                    img = await _imageService.GetProjectCollectionArtworkPlacementImageAsync(projectId, collectionId, itemId, artworkId, idx);
                return img;
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
                        // Pre-load placement variants for all artworks
                        var artworkPlacementsMap = new Dictionary<Guid, List<ProjectCollectionArtworkPlacement>>();
                        foreach (var a in collectionArtwork.Where(a => a.Active))
                        {
                            if (!artworkPlacementsMap.ContainsKey(a.Id))
                                artworkPlacementsMap[a.Id] = (await _projectCollectionArtworkPlacementRepository.GetByArtworkIdAsync(a.Id)).ToList();
                        }

                        foreach (var placement in placementArr)
                        {
                            var pItemId = placement.GetItemId();
                            if (pItemId == Guid.Empty) continue;

                            var pArtwork = collectionArtwork.FirstOrDefault(a => a.ItemId == pItemId && a.Active);
                            if (pArtwork == null) continue;

                            var (pw, ph) = placement.GetDimensions();
                            var placementVariants = artworkPlacementsMap.GetValueOrDefault(pArtwork.Id, new List<ProjectCollectionArtworkPlacement>());

                            var pImgBytes = await GetPlacementSpecificArtworkAsync(
                                request.ProjectId, request.CollectionId, pItemId, pArtwork.Id, pArtwork.Opacity,
                                placement.Position ?? "", pw, ph, placementVariants);

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
                    request.CollectionId, request.ProjectBlueprintId, request.ProductImageId, activeOnly: false);

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
                await _imageService.GenerateProjectCollectionProductImageThumbAsync(request.ProjectId, request.CollectionId, productImage.Id);

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
                        accepted = productImage.Accepted,
                        active = productImage.Active
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
                var blueprint = await _projectBlueprintRepository.GetByIdAsync(projectBlueprintId);
                var variants = (blueprint != null && blueprint.BlueprintId > 0)
                    ? (await _variantRepository.GetByBlueprintIdAsync(blueprint.BlueprintId)).ToList()
                    : new List<PrintifyBlueprintVariant>();
                var variantIdsByColor = variants
                    .GroupBy(v => v.Color, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Select(v => v.VariantId).ToList(), StringComparer.OrdinalIgnoreCase);

                return Json(new ApiResponse
                {
                    success = true,
                    data = images.Select(img =>
                    {
                        variantIdsByColor.TryGetValue(img.VariantColor, out var variantIds);
                        return new
                        {
                            id = img.Id,
                            projectBlueprintId = img.ProjectBlueprintId,
                            title = img.Title,
                            variantColor = img.VariantColor,
                            variantIds = variantIds ?? new List<int>(),
                            status = img.Status,
                            prompt = img.Prompt,
                            imageId = img.ImageId
                        };
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
                var blueprintMap = blueprints.ToDictionary(b => b.Id);
                var images = await _projectBlueprintProductImageRepository.GetByBlueprintIdsAsync(blueprintIds);

                var printifyBlueprintIds = blueprints.Select(b => b.BlueprintId).Where(id => id > 0).Distinct().ToList();
                var allVariants = printifyBlueprintIds.Count > 0
                    ? (await _variantRepository.GetByBlueprintIdsAsync(printifyBlueprintIds)).ToList()
                    : new List<PrintifyBlueprintVariant>();
                var variantsByBlueprint = allVariants.GroupBy(v => v.BlueprintId).ToDictionary(g => g.Key, g => g.ToList());

                return Json(new ApiResponse
                {
                    success = true,
                    data = images.Select(img =>
                    {
                        var bp = blueprintMap.TryGetValue(img.ProjectBlueprintId, out var b) ? b : null;
                        var bpId = bp?.BlueprintId ?? 0;
                        var variants = variantsByBlueprint.TryGetValue(bpId, out var vs) ? vs : new List<PrintifyBlueprintVariant>();
                        var variantIds = variants
                            .Where(v => string.Equals(v.Color, img.VariantColor, StringComparison.OrdinalIgnoreCase))
                            .Select(v => v.VariantId)
                            .ToList();
                        return new
                        {
                            id = img.Id,
                            projectBlueprintId = img.ProjectBlueprintId,
                            blueprintName = blueprintNameMap.TryGetValue(img.ProjectBlueprintId, out var name) ? name : "",
                            title = img.Title,
                            variantColor = img.VariantColor,
                            variantIds,
                            status = img.Status,
                            prompt = img.Prompt,
                            imageId = img.ImageId
                        };
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

        [HttpPost("save-collection-products")]
        public async Task<IActionResult> SaveCollectionProducts([FromBody] SaveCollectionProductsRequest request)
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

                var project = await _projectRepository.GetByIdAsync(collection.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Collection not found." });

                var blueprints = await _projectBlueprintRepository.GetByProjectIdAsync(collection.ProjectId);
                var blueprintDict = blueprints.ToDictionary(b => b.Id);

                foreach (var sel in request.Products)
                {
                    if (!blueprintDict.TryGetValue(sel.ProjectBlueprintId, out var bp)) continue;

                    var existing = await _productRepository.GetByCollectionAndBlueprintIdAsync(request.CollectionId, bp.Id);
                    if (existing == null)
                    {
                        await _productRepository.CreateAsync(new ProjectCollectionProduct
                        {
                            ProjectId = collection.ProjectId,
                            CollectionId = request.CollectionId,
                            ProjectBlueprintId = bp.Id,
                            BlueprintId = bp.BlueprintId,
                            Name = bp.Name,
                            Description = bp.Description ?? "",
                            SafetyInfo = bp.SafetyInfo ?? "",
                            PricingJson = bp.PricingJson ?? "[]",
                            Active = sel.Active
                        });
                    }
                    else
                    {
                        existing.Active = sel.Active;
                        await _productRepository.UpdateAsync(existing);
                    }
                }

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
