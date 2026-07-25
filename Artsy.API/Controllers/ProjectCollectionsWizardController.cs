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
                        width = a.Width,
                        height = a.Height
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
                if (artwork == null || string.IsNullOrWhiteSpace(artwork.ImageModel))
                    return Json(new ApiResponse { success = false, message = "No image model configured for this item." });

                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                };

                var genModel = await _imageGenerationModelRepository.GetByModelKeyAsync(artwork.ImageModel);
                if (genModel == null)
                    return Json(new ApiResponse { success = false, message = "Image model not found in database." });

                var modelRequest = new OpenAIImageRequest();
                modelRequest.Model = genModel.Model;

                var promptBuilder = new StringBuilder(artwork.Prompt ?? "");

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

                if (!string.IsNullOrWhiteSpace(request.RequestedChanges))
                {
                    promptBuilder.AppendLine();
                    promptBuilder.AppendLine($"Requested Changes: {request.RequestedChanges}");
                }

                var finalPrompt = promptBuilder.ToString().Trim();
                if (string.IsNullOrWhiteSpace(finalPrompt))
                    return Json(new ApiResponse { success = false, message = "Prompt is required to generate artwork." });

                modelRequest.Prompt = finalPrompt;

                var blueprints = await _projectBlueprintRepository.GetByProjectIdAsync(request.ProjectId);
                var placementDims = new List<(int W, int H)>();
                foreach (var bp in blueprints)
                {
                    if (string.IsNullOrWhiteSpace(bp.PlacementJson)) continue;
                    try
                    {
                        var placements = JsonSerializer.Deserialize<Dictionary<string, PlacementDto>>(bp.PlacementJson);
                        if (placements == null) continue;
                        foreach (var p in placements.Values)
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
                    Prompt = finalPrompt
                };
                var created = await _projectCollectionArtworkRepository.UpsertAsync(collectionArtwork);

                try
                {
                    var imageGen = _imageGenerations.FirstOrDefault(g => g.ModelKey.Equals(artwork.ImageModel, StringComparison.OrdinalIgnoreCase));
                    if (imageGen == null)
                        throw new InvalidOperationException($"Image model '{artwork.ImageModel}' is not supported.");

                    string? previousResponseId = null;
                    if (!string.IsNullOrWhiteSpace(request.RequestedChanges) && !string.IsNullOrWhiteSpace(created.ResponseId))
                    {
                        previousResponseId = created.ResponseId;
                    }

                    var genQuality = request.IsFullSize ? "high" : "medium";
                    var genResult = await imageGen.GenerateAsync(artwork.ImageModel, imageModelJson, genQuality, previousResponseId, true);
                    if (request.IsFullSize)
                        await _imageService.SaveProjectCollectionArtworkFullSizeAsync(request.ProjectId, request.CollectionId, request.ItemId, created.Id, genResult.ImageBytes);
                    else
                        await _imageService.SaveProjectCollectionArtworkAsync(request.ProjectId, request.CollectionId, request.ItemId, created.Id, genResult.ImageBytes);

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
                        InputTextTokens = genResult.InputTokens,
                        InputImageTokens = 0,
                        OutputTokens = genResult.OutputTokens,
                        ImageModel = genModel.Model,
                        Prompt = finalPrompt,
                        Filename = request.IsFullSize ? $"{created.Id}_fullsize.jpg" : $"{created.Id}.jpg",
                        IsFullSize = request.IsFullSize
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

                var previewBytes = await _imageService.GetProjectCollectionArtworkImageAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id);
                if (previewBytes == null || previewBytes.Length == 0)
                    return Json(new ApiResponse { success = false, message = "Preview image data not found." });

                var upscaledBytes = await _imageUpscaler.UpscaleAsync(previewBytes);
                await _imageService.SaveProjectCollectionArtworkFullSizeAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, upscaledBytes);

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

        [HttpGet("collection/{collectionId}/item/{itemId}/artwork/{artworkId}")]
        public async Task<IActionResult> GetCollectionArtworkImage(Guid collectionId, Guid itemId, Guid artworkId, [FromQuery] bool fullSize = false)
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

                var bytes = fullSize
                    ? await _imageService.GetProjectCollectionArtworkFullSizeAsync(artwork.ProjectId, collectionId, itemId, artworkId)
                    : await _imageService.GetProjectCollectionArtworkImageAsync(artwork.ProjectId, collectionId, itemId, artworkId);
                if (bytes == null || bytes.Length == 0)
                    return NotFound();

                return File(bytes, "image/jpeg");
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
                        var placementsDict = JsonSerializer.Deserialize<Dictionary<string, PlacementDto>>(bp.PlacementJson);
                        if (placementsDict == null) continue;

                        foreach (var placement in placementsDict.Values)
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

                var variantsByBlueprint = allVariants.GroupBy(v => v.BlueprintId).ToDictionary(g => g.Key, g => g.ToList());
                var imagesByBlueprint = allImages.GroupBy(i => i.BlueprintId).ToDictionary(g => g.Key, g => g.ToList());

                var existingProductImages = collectionId != Guid.Empty
                    ? (await _projectCollectionProductImageRepository.GetByCollectionIdAsync(collectionId)).ToList()
                    : new List<ProjectCollectionProductImage>();

                var printifyBlueprintsById = printifyBlueprintIds.Count > 0
                    ? (await _printifyBlueprintRepository.GetByBlueprintIdsAsync(printifyBlueprintIds)).ToDictionary(b => b.BlueprintId)
                    : new Dictionary<int, PrintifyBlueprint>();

                var genModel = await _imageGenerationModelRepository.GetByModelKeyAsync("openai");
                var conversion = genModel != null && genModel.TokenConversion > 0 ? (1000m * genModel.TokenConversion) : 1000m;

                IImageTokens? tokenizer = null;
                if (genModel != null)
                {
                    var imageGen = _imageGenerations.FirstOrDefault(g => g.ModelKey.Equals("openai", StringComparison.OrdinalIgnoreCase));
                    if (imageGen != null)
                        tokenizer = imageGen.CreateTokenizer(genModel);
                }

                var printifyImageDimensionsCache = new Dictionary<(int, int), (int width, int height)>();

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

                    var variantTitles = new Dictionary<int, string>();
                    var colorsSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (printifyBlueprintId > 0 && variantsByBlueprint.TryGetValue(printifyBlueprintId, out var bpVariants))
                    {
                        foreach (var v in bpVariants)
                        {
                            if (selectedVariantIds.Count > 0 && !selectedVariantIds.Contains(v.VariantId))
                                continue;

                            var color = v.Title;
                            try
                            {
                                if (!string.IsNullOrWhiteSpace(v.Options))
                                {
                                    using var optsDoc = JsonDocument.Parse(v.Options);
                                    if (optsDoc.RootElement.TryGetProperty("color", out var colorEl))
                                        color = colorEl.GetString() ?? v.Title;
                                }
                            }
                            catch { }

                            if (colorsSeen.Add(color))
                                variantTitles[v.VariantId] = color;
                        }
                    }

                    var placements = new List<object>();
                    var placementKeys = new List<string>();
                    if (!string.IsNullOrWhiteSpace(bp.PlacementJson))
                    {
                        try
                        {
                            var placementDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(bp.PlacementJson);
                            if (placementDict != null)
                            {
                                int placementIndex = 0;
                                foreach (var kv in placementDict)
                                {
                                    var placementName = kv.Key;
                                    var placementObj = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(kv.Value.ToString() ?? "{}");
                                    if (placementObj == null) continue;

                                    placementKeys.Add(placementName);

                                    var source = placementObj.TryGetValue("source", out var sVal) ? sVal.ToString() : "";
                                    var itemId = placementObj.TryGetValue("itemId", out var iVal) ? iVal.ToString() : "";

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
                    var prompt = !string.IsNullOrWhiteSpace(existingProductImage?.Prompt)
                        ? existingProductImage.Prompt
                        : bp.Prompt ?? "";

                    var combinedPrompt = $"{imagePrompt}\n{prompt}".Trim();

                    var variants = new List<object>();
                    var variantIdsAdded = new HashSet<int>();
                    if (printifyBlueprintId > 0 && imagesByBlueprint.TryGetValue(printifyBlueprintId, out var bpImages))
                    {
                        foreach (var img in bpImages)
                        {
                            var imgVariants = System.Text.Json.JsonSerializer.Deserialize<int[]>(img.Variants ?? "[]") ?? Array.Empty<int>();
                            foreach (var v in imgVariants)
                            {
                                if (!variantTitles.ContainsKey(v))
                                    continue;
                                if (variantIdsAdded.Add(v))
                                {
                                    var variantImages = bpImages.Where(img2 =>
                                    {
                                        var img2Variants = System.Text.Json.JsonSerializer.Deserialize<int[]>(img2.Variants ?? "[]") ?? Array.Empty<int>();
                                        return img2Variants.Contains(v);
                                    }).ToList();

                                    var hasForProductImage = variantImages.Any(i => i.Type == 3);
                                    var hasBefore = variantImages.Any(i => i.Type == 1);
                                    var hasAfter = variantImages.Any(i => i.Type == 2);
                                    var hasNone = variantImages.Any(i => i.Type == 0);

                                    var selectedPrintifyImages = new List<PrintifyBlueprintImage>();
                                    if (hasForProductImage)
                                        selectedPrintifyImages.Add(variantImages.First(i => i.Type == 3));
                                    else if (hasBefore && hasAfter)
                                    {
                                        selectedPrintifyImages.Add(variantImages.First(i => i.Type == 1));
                                        selectedPrintifyImages.Add(variantImages.First(i => i.Type == 2));
                                    }
                                    else if (hasBefore)
                                        selectedPrintifyImages.Add(variantImages.First(i => i.Type == 1));
                                    else if (hasAfter)
                                        selectedPrintifyImages.Add(variantImages.First(i => i.Type == 2));
                                    else if (hasNone)
                                        selectedPrintifyImages.Add(variantImages.First(i => i.Type == 0));

                                    var printifyDims = new List<(int width, int height)>();
                                    foreach (var pImg in selectedPrintifyImages.Take(2))
                                    {
                                        var cacheKey = (printifyBlueprintId, pImg.ImageIndex);
                                        if (!printifyImageDimensionsCache.TryGetValue(cacheKey, out var dims))
                                        {
                                            var imgBytes = await _imageService.GetPrintifyCatalogImageAsync(printifyBlueprintId, pImg.ImageIndex);
                                            var dimResult = await _imageService.GetImageDimensionsAsync(imgBytes);
                                            dims = dimResult ?? (1024, 1024);
                                            printifyImageDimensionsCache[cacheKey] = dims;
                                        }
                                        printifyDims.Add(dims);
                                    }

                                    var combos = new List<object>();
                                    foreach (var p in placements)
                                    {
                                        dynamic placement = p;
                                        int inputImageCount = selectedPrintifyImages.Count + 1;

                                        var inputImages = new List<(int width, int height)>(printifyDims);

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
                                            var tokenResult = tokenizer.CalculateTokens(combinedPrompt, 2048, 2048, "medium", inputImages);
                                            comboTokens = (int)Math.Round((tokenResult.TextInputTokens + tokenResult.ImageInputTokens + tokenResult.ImageOutputTokens) / conversion);
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
                                        variant = v,
                                        variantTitle = variantTitles[v],
                                        imageIndex = img.ImageIndex,
                                        type = img.Type,
                                        position = img.Position,
                                        imageUrl = $"/api/printify/blueprint-image?blueprintId={printifyBlueprintId}&index={img.ImageIndex}",
                                        combos
                                    });
                                }
                            }
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

                var placementItemId = Guid.Empty;
                try
                {
                    var placementDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(bp.PlacementJson ?? "{}");
                    if (placementDict != null)
                    {
                        var placementKeys = placementDict.Keys.ToList();
                        if (request.Placement >= 0 && request.Placement < placementKeys.Count)
                        {
                            var placementKey = placementKeys[request.Placement];
                            if (placementDict.TryGetValue(placementKey, out var pVal))
                            {
                                var placementObj = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(pVal.ToString() ?? "{}");
                                if (placementObj != null && placementObj.TryGetValue("itemId", out var iVal))
                                    Guid.TryParse(iVal.ToString(), out placementItemId);
                            }
                        }
                    }
                }
                catch { }

                var artwork = collectionArtwork.FirstOrDefault(a => a.ItemId == placementItemId && a.Active);
                if (artwork == null)
                    return Json(new ApiResponse { success = false, message = "No accepted artwork found for this placement." });

                var artworkImageBytes = await _imageService.GetProjectCollectionArtworkImageAsync(request.ProjectId, request.CollectionId, placementItemId, artwork.Id);
                if (artworkImageBytes == null || artworkImageBytes.Length == 0)
                    return Json(new ApiResponse { success = false, message = "Artwork image not found." });

                var promptBuilder = new StringBuilder();
                promptBuilder.AppendLine("Apply the following artwork design onto the product shown in the reference image.");
                promptBuilder.AppendLine("Place the product in a realistic, appealing scenario as described below.");
                if (!string.IsNullOrWhiteSpace(printifyBlueprint?.ImagePrompt))
                {
                    promptBuilder.AppendLine();
                    promptBuilder.AppendLine($"Product context: {printifyBlueprint.ImagePrompt}");
                }
                if (!string.IsNullOrWhiteSpace(bp.Prompt))
                {
                    promptBuilder.AppendLine();
                    promptBuilder.AppendLine($"Additional instructions: {bp.Prompt}");
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

                var genModel = await _imageGenerationModelRepository.GetByModelKeyAsync("openai");
                if (genModel == null)
                    return Json(new ApiResponse { success = false, message = "Image model not found in database." });

                var modelRequest = new OpenAIImageRequest
                {
                    Model = genModel.Model,
                    Prompt = finalPrompt,
                    Size = "2048x2048",
                    Quality = "medium",
                    Images = new List<OpenAIImageReference>()
                };

                if (printifyBlueprintId > 0)
                {
                    var bpImages = await _printifyBlueprintImageRepository.GetByBlueprintIdAsync(printifyBlueprintId);
                    var variantImages = bpImages.Where(img =>
                    {
                        var imgVariants = System.Text.Json.JsonSerializer.Deserialize<int[]>(img.Variants ?? "[]") ?? Array.Empty<int>();
                        return imgVariants.Contains(request.Variant);
                    }).ToList();

                    var selectedPrintifyImages = new List<PrintifyBlueprintImage>();

                    var forProductImage = variantImages.FirstOrDefault(img => img.Type == 3);
                    if (forProductImage != null)
                    {
                        selectedPrintifyImages.Add(forProductImage);
                    }
                    else
                    {
                        var beforeImage = variantImages.FirstOrDefault(img => img.Type == 1);
                        var afterImage = variantImages.FirstOrDefault(img => img.Type == 2);

                        if (beforeImage != null && afterImage != null)
                        {
                            selectedPrintifyImages.Add(beforeImage);
                            selectedPrintifyImages.Add(afterImage);
                        }
                        else if (beforeImage != null)
                        {
                            selectedPrintifyImages.Add(beforeImage);
                        }
                        else if (afterImage != null)
                        {
                            selectedPrintifyImages.Add(afterImage);
                        }
                        else
                        {
                            var noneImage = variantImages.FirstOrDefault(img => img.Type == 0);
                            if (noneImage != null)
                                selectedPrintifyImages.Add(noneImage);
                        }
                    }

                    foreach (var pImg in selectedPrintifyImages.Take(2))
                    {
                        var imgBytes = await _imageService.GetPrintifyCatalogImageAsync(printifyBlueprintId, pImg.ImageIndex);
                        if (imgBytes != null && imgBytes.Length > 0)
                        {
                            modelRequest.Images.Add(new OpenAIImageReference
                            {
                                Image = Convert.ToBase64String(imgBytes),
                                Detail = "auto"
                            });
                        }
                    }
                }

                modelRequest.Images.Add(new OpenAIImageReference
                {
                    Image = Convert.ToBase64String(artworkImageBytes),
                    Detail = "auto"
                });

                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                };
                var imageModelJson = JsonSerializer.Serialize(modelRequest, jsonOptions);

                var imageGen = _imageGenerations.FirstOrDefault(g => g.ModelKey.Equals("openai", StringComparison.OrdinalIgnoreCase));
                if (imageGen == null)
                    return Json(new ApiResponse { success = false, message = "Image generation service not available." });

                var existing = await _projectCollectionProductImageRepository.GetByCollectionBlueprintVariantPlacementAsync(
                    request.CollectionId, request.ProjectBlueprintId, request.Variant, request.Placement);

                string? previousResponseId = null;
                if (existing != null && !string.IsNullOrWhiteSpace(existing.ResponseId) && !string.IsNullOrWhiteSpace(request.RequestedChanges))
                {
                    previousResponseId = existing.ResponseId;
                }

                var genResult = await imageGen.GenerateAsync("openai", imageModelJson, "medium", previousResponseId, true);

                var productImage = new ProjectCollectionProductImage
                {
                    ProjectId = request.ProjectId,
                    CollectionId = request.CollectionId,
                    ProjectBlueprintId = request.ProjectBlueprintId,
                    Variant = request.Variant,
                    Placement = request.Placement,
                    ImageModel = genModel.Model,
                    Prompt = finalPrompt,
                    Width = 2048,
                    Height = 2048,
                    Accepted = false,
                    ResponseId = genResult.ResponseId ?? ""
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

                await _projectImageGenerationRepository.CreateAsync(new ProjectImageGeneration
                {
                    ProjectId = request.ProjectId,
                    CollectionId = request.CollectionId,
                    InputTextTokens = genResult.InputTokens,
                    InputImageTokens = 0,
                    OutputTokens = genResult.OutputTokens,
                    ImageModel = genModel.Model,
                    Prompt = finalPrompt,
                    Filename = $"{productImage.Id}.jpg"
                });

                return Json(new ApiResponse
                {
                    success = true,
                    data = new
                    {
                        id = productImage.Id,
                        projectBlueprintId = productImage.ProjectBlueprintId,
                        variant = productImage.Variant,
                        placement = productImage.Placement,
                        imageUrl = $"/api/projects/collection/{request.CollectionId}/product-image/{productImage.Id}",
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
                        variant = img.Variant,
                        placement = img.Placement,
                        accepted = img.Accepted,
                        imageUrl = $"/api/projects/collection/{collectionId}/product-image/{img.Id}"
                    })
                });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("collection/{collectionId}/product-image/{productImageId}")]
        public async Task<IActionResult> GetProductImageFile(Guid collectionId, Guid productImageId)
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

                var bytes = await _imageService.GetProjectCollectionProductImageAsync(productImage.ProjectId, collectionId, productImageId);
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

    }
}
