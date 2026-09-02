using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Artsy.API.Models;
using Artsy.API.Models.Projects;
using Artsy.API.Models.Collections;
using Artsy.API.Models.Printify;
using Artsy.API.Services;
using Artsy.AI;
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

        [HttpGet("load-collection-wizard")]
        public async Task<IActionResult> LoadCollectionWizard([FromQuery] Guid projectId, [FromQuery] Guid? collectionId = null)
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

                // Single SQL query with multiple result sets
                var data = await _collectionWizardRepository.LoadAsync(projectId, collectionId);

                // ── Items (same format as get-items, with thumbnail URLs) ──
                var artworkByItem = data.ItemArtwork.ToDictionary(a => a.ItemId, a => a);
                var refThumbsByItem = data.RefThumbnails.GroupBy(r => r.ItemId).ToDictionary(g => g.Key, g => g.ToList());
                var previewThumbsByItem = data.PreviewThumbnails.GroupBy(p => p.ItemId).ToDictionary(g => g.Key, g => g.ToList());

                var itemsResult = data.Items.Select(i =>
                {
                    var thumbnails = new List<string>();
                    if (artworkByItem.TryGetValue(i.Id, out var art) && art.ArtworkType == "custom" && art.CustomImageId.HasValue)
                    {
                        thumbnails.Add($"/api/custom-images/custom-image/{art.CustomImageId.Value}?thumb=true");
                    }
                    else
                    {
                        if (previewThumbsByItem.TryGetValue(i.Id, out var previews))
                            foreach (var p in previews)
                                thumbnails.Add($"/api/projects/item/{i.Id}/preview/{p.Id}?thumb=true");
                        if (refThumbsByItem.TryGetValue(i.Id, out var refs))
                            foreach (var r in refs)
                                thumbnails.Add($"/api/projects/item/{i.Id}/reference/{r.Id}?thumb=true");
                    }
                    return new ProjectItemListItem
                    {
                        Id = i.Id,
                        ProjectId = i.ProjectId,
                        Index = i.Index,
                        Title = i.Title,
                        SocialMedia = i.SocialMedia,
                        ProductCount = i.ProductCount,
                        QuestionCount = i.QuestionCount,
                        ArtworkType = artworkByItem.TryGetValue(i.Id, out var artwork) ? artwork.ArtworkType : "ai",
                        OpacityJson = artworkByItem.TryGetValue(i.Id, out var aw) ? aw.OpacityJson : null,
                        Thumbnails = thumbnails
                    };
                }).ToList();

                // ── Blueprints (same format as get-blueprints) ──
                var bpProductImages = data.BlueprintProductImages;
                var imagesByBlueprint = bpProductImages.GroupBy(img => img.ProjectBlueprintId).ToDictionary(g => g.Key, g => g.ToList());
                var printifyImagesByBlueprint = data.PrintifyImages.GroupBy(img => img.BlueprintId).ToDictionary(g => g.Key, g => g.ToList());
                var printifyImageVariantsByImageId = data.PrintifyImageVariants.GroupBy(v => v.BlueprintImageId).ToDictionary(g => g.Key, g => g.Select(v => v.VariantColor).ToList());

                foreach (var b in data.Blueprints)
                {
                    var bpImages = imagesByBlueprint.TryGetValue(b.Id, out var imgs) ? imgs : null;
                    b.Configured = IsBlueprintConfigured(b.Name, b.Description, b.BlueprintJson, b.PlacementJson, b.PricingJson, bpImages);
                }

                var blueprintsResult = data.Blueprints.Select(b => new
                {
                    b.Id,
                    b.BlueprintId,
                    b.Name,
                    b.BlueprintJson,
                    b.PlacementJson,
                    b.Prompt,
                    b.Description,
                    b.SafetyInfo,
                    b.PricingJson,
                    b.PrintProviderId,
                    b.Configured,
                    b.ImageCount,
                    minPrice = GetMinPriceFromPricingJson(b.PricingJson),
                    printifyImages = printifyImagesByBlueprint.TryGetValue(b.BlueprintId, out var pImgs)
                        ? pImgs.Select(img => new { variantColors = printifyImageVariantsByImageId.TryGetValue(img.Id, out var colors) ? colors : new List<string>(), img.ImageIndex }).Cast<object>()
                        : Enumerable.Empty<object>()
                }).ToList();

                // Printify image index by color map
                var printifyImageIndexColorMap = new Dictionary<Guid, Dictionary<string, int>>();
                foreach (var b in data.Blueprints)
                {
                    var idxMap = new Dictionary<string, int>();
                    if (printifyImagesByBlueprint.TryGetValue(b.BlueprintId, out var pImgs))
                    {
                        foreach (var img in pImgs)
                        {
                            if (printifyImageVariantsByImageId.TryGetValue(img.Id, out var colors))
                            {
                                foreach (var color in colors)
                                {
                                    idxMap[color] = img.ImageIndex;
                                }
                            }
                        }
                    }
                    printifyImageIndexColorMap[b.Id] = idxMap;
                }

                // ── Item references ──
                var itemRefsResult = data.ItemReferences.Select(r => new
                {
                    id = r.Id,
                    itemId = r.ItemId,
                    artworkId = r.ArtworkId
                }).ToList();

                // Questions (returned as raw entities, same as get-questions)
                var questionsResult = data.Questions;

                // ── Collection-specific data ──
                object answersResult = null;
                object artworkResult = null;
                object printifyProductsResult = null;
                object mockupsResult = null;
                bool instagramPosted = false;
                object instagramPost = null;
                object collectionProductsResult = null;
                object productBlueprintImagesResult = null;
                object productImagesResult = null;

                if (collectionId.HasValue && collectionId.Value != Guid.Empty && data.Answers != null)
                {
                    var colId = collectionId.Value;

                    // Answers
                    answersResult = data.Answers.Select(a => new
                    {
                        id = a.Id,
                        questionId = a.QuestionId,
                        itemId = a.ItemId,
                        answer = a.Answer
                    }).ToList();

                    // Artwork (same format as get-collection-artwork, with needsRegeneration check)
                    var artworkObjects = new List<object>();
                    var placementsByArtwork = data.ArtworkPlacements.GroupBy(p => p.CollectionArtworkId).ToDictionary(g => g.Key, g => g.ToList());
                    foreach (var a in data.Artwork)
                    {
                        var placements = placementsByArtwork.TryGetValue(a.Id, out var ap) ? ap : new List<ProjectCollectionArtworkPlacement>();

                        bool needsRegeneration = false;
                        try
                        {
                            var plan = await _artworkGenerationPlanService.BuildPlanAsync(projectId, colId, a.ItemId, resolutionTier: 2, design: a.Design ?? "artwork");
                            needsRegeneration = a.TotalPlacements != plan.TotalPlacements;
                        }
                        catch { }

                        var groupPlacements = placements
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
                                    printifyImageId = p.PrintifyImageId,
                                    optionalPrompt = p.OptionalPrompt
                                }).ToList()
                            }).ToList();

                        artworkObjects.Add(new
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
                            design = a.Design,
                            patternJson = a.PatternJson,
                            optionalPrompt = a.OptionalPrompt,
                            needsRegeneration,
                            hasGroups = groupPlacements.Count > 0,
                            groupPlacements,
                            placements = placements.Select(p => new
                            {
                                id = p.Id,
                                width = p.Width,
                                height = p.Height,
                                index = p.Index,
                                fullSize = p.FullSize,
                                printifyImageId = p.PrintifyImageId,
                                groupId = p.GroupId,
                                position = p.Position,
                                optionalPrompt = p.OptionalPrompt
                            })
                        });
                    }
                    artworkResult = artworkObjects;

                    // Printify products (same format as get-by-collection)
                    var products = data.CollectionProducts;
                    var mockupsByPpId = data.Mockups.GroupBy(m => m.PrintifyProductId).ToDictionary(g => g.Key, g => g.Count());
                    var productMap = products.ToDictionary(p => p.Id);
                    printifyProductsResult = data.PrintifyProducts.Select(pp => new
                    {
                        pp.Id,
                        pp.ProjectId,
                        pp.CollectionId,
                        pp.ProductId,
                        pp.PrintifyProductId,
                        pp.PrintifyShopId,
                        pp.PrintifyUserId,
                        pp.ProviderId,
                        pp.Published,
                        pp.Status,
                        pp.Created,
                        ProjectBlueprintId = productMap.TryGetValue(pp.ProductId, out var p) ? p.ProjectBlueprintId : Guid.Empty,
                        BlueprintName = productMap.TryGetValue(pp.ProductId, out p) ? p.Name : "",
                        MockupsDownloaded = mockupsByPpId.TryGetValue(pp.Id, out var mockupCount) && mockupCount > 0,
                    }).ToList();

                    // Mockups (same format as get-mockups, with URL instead of image data)
                    mockupsResult = data.Mockups.Select(m => new
                    {
                        m.Id,
                        m.ProjectId,
                        m.CollectionId,
                        m.PrintifyProductId,
                        m.VariantIds,
                        m.Position,
                        m.IsDefault,
                        m.Status,
                        ImageUrl = $"/api/printify-products/mockup-image?projectId={m.ProjectId}&collectionId={m.CollectionId}&mockupId={m.Id}&thumb=true",
                    }).ToList();

                    // Instagram
                    instagramPosted = data.InstagramPosts.Any();
                    var igPost = data.InstagramPosts.FirstOrDefault();
                    if (igPost != null)
                    {
                        instagramPost = new
                        {
                            id = igPost.Id,
                            description = igPost.Description,
                            permalink = igPost.Permalink,
                            created = igPost.Created
                        };
                    }

                    // Collection products (same format as get-collection-products)
                    collectionProductsResult = products.Select(p => new
                    {
                        id = p.Id,
                        projectId = p.ProjectId,
                        collectionId = p.CollectionId,
                        projectBlueprintId = p.ProjectBlueprintId,
                        blueprintId = p.BlueprintId,
                        name = p.Name,
                        active = p.Active
                    }).ToList();

                    // Product blueprint images (same format as get-all-product-blueprint-images)
                    var pbImages = bpProductImages.Where(bpi => bpi.Status == 1).ToList();
                    var variantsByBp = data.PrintifyVariants.GroupBy(v => v.BlueprintId).ToDictionary(g => g.Key, g => g.ToList());
                    var blueprintMap = data.Blueprints.ToDictionary(b => b.Id);
                    var blueprintNameMap = data.Blueprints.ToDictionary(b => b.Id, b => b.Name);
                    productBlueprintImagesResult = pbImages.Select(img =>
                    {
                        var bp = blueprintMap.TryGetValue(img.ProjectBlueprintId, out var b) ? b : null;
                        var bpId = bp?.BlueprintId ?? 0;
                        var variants = variantsByBp.TryGetValue(bpId, out var vs) ? vs : new List<PrintifyBlueprintVariant>();
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
                    }).ToList();

                    // Product images (same format as collection/{collectionId}/product-images)
                    var prodImages = data.ProductImages;
                    var existingBpImageIds = new HashSet<Guid>(
                        prodImages.Where(img => img.ProductImageId != Guid.Empty).Select(img => img.ProductImageId)
                    );
                    var newImages = new List<ProjectCollectionProductImage>();
                    foreach (var bpi in pbImages)
                    {
                        if (existingBpImageIds.Contains(bpi.Id)) continue;
                        var newImg = await _projectCollectionProductImageRepository.CreateAsync(new ProjectCollectionProductImage
                        {
                            ProjectId = projectId,
                            CollectionId = colId,
                            ProjectBlueprintId = bpi.ProjectBlueprintId,
                            ProductImageId = bpi.Id,
                            ImageModel = "",
                            Prompt = bpi.Prompt ?? "",
                            Width = 0,
                            Height = 0,
                            Accepted = false,
                            ResponseId = "",
                            VariantColor = bpi.VariantColor ?? "",
                            Active = true,
                            SelectedMockups = "",
                            Generated = false
                        });
                        newImages.Add(newImg);
                    }
                    var allProdImages = prodImages.Concat(newImages).ToList();
                    var bpTitleMap = pbImages.ToDictionary(bpi => bpi.Id, bpi => bpi.Title ?? "");
                    var productNameMap = products.ToDictionary(p => p.ProjectBlueprintId, p => p.Name ?? "");
                    productImagesResult = allProdImages.Select(img => new
                    {
                        id = img.Id,
                        projectBlueprintId = img.ProjectBlueprintId,
                        productImageId = img.ProductImageId,
                        accepted = img.Accepted,
                        active = img.Active,
                        prompt = img.Prompt,
                        imageModel = img.ImageModel,
                        variantColor = img.VariantColor,
                        title = img.ProjectBlueprintId.HasValue && productNameMap.TryGetValue(img.ProjectBlueprintId.Value, out var pname) ? pname : "",
                        subtitle = img.ProductImageId != Guid.Empty && bpTitleMap.TryGetValue(img.ProductImageId, out var t) ? t : (img.VariantColor ?? ""),
                        selectedMockups = img.SelectedMockups,
                        generated = img.Generated,
                        includeArtworkRef = img.IncludeArtworkRef,
                        imageUrl = img.Generated ? $"/api/projects/collection/{colId}/product-image/{img.Id}?thumb=true" : null
                    }).ToList();
                }

                return Json(new ApiResponse
                {
                    success = true,
                    data = new
                    {
                        questions = questionsResult,
                        items = itemsResult,
                        blueprints = blueprintsResult,
                        itemReferences = itemRefsResult,
                        printifyImageIndexByColor = printifyImageIndexColorMap,
                        answers = answersResult,
                        artwork = artworkResult,
                        printifyProducts = printifyProductsResult,
                        mockups = mockupsResult,
                        instagramPosted,
                        instagramPost,
                        collectionProducts = collectionProductsResult,
                        productBlueprintImages = productBlueprintImagesResult,
                        productImages = productImagesResult
                    }
                });
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
                        var plan = await _artworkGenerationPlanService.BuildPlanAsync(collection.ProjectId, collectionId, a.ItemId, resolutionTier: 2, design: a.Design ?? "artwork");
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
                                printifyImageId = p.PrintifyImageId,
                                optionalPrompt = p.OptionalPrompt
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
                        design = a.Design,
                        patternJson = a.PatternJson,
                        optionalPrompt = a.OptionalPrompt,
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
                            position = p.Position,
                            optionalPrompt = p.OptionalPrompt
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
                // Pass the placement optional prompt so the plan can use it instead of the artwork-level prompt
                var plan = await _artworkGenerationPlanService.BuildPlanAsync(
                    request.ProjectId, request.CollectionId, request.ItemId,
                    request.RequestedChanges, request.Answers, resolutionTier: 2,
                    design: request.Design ?? "artwork",
                    placementOptionalPrompt: request.PlacementOptionalPrompt);

                if (string.IsNullOrWhiteSpace(plan.FinalPrompt))
                    return Json(new ApiResponse { success = false, message = "Prompt is required to generate artwork." });

                // For pattern design mode, append seamless repeating pattern instructions to the prompt
                var isPattern = string.Equals(request.Design, "pattern", StringComparison.OrdinalIgnoreCase);
                if (isPattern)
                {
                    plan.FinalPrompt += ". Design this as a seamless repeating pattern that tiles perfectly without visible seams or borders. The artwork should be a continuous tileable pattern that can be repeated horizontally and vertically.";
                }

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

                // When regenerating a specific placement, preserve FullSize and Accepted from the existing artwork
                var isFirstGeneration = request.GenerationIndex == null;
                var existingArtwork = (await _projectCollectionArtworkRepository.GetByCollectionIdAsync(request.CollectionId))
                    .FirstOrDefault(a => a.ItemId == request.ItemId);

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
                    TotalPlacements = plan.TotalPlacements,
                    FullSize = !isFirstGeneration && existingArtwork != null ? existingArtwork.FullSize : false,
                    Accepted = !isFirstGeneration && existingArtwork != null ? existingArtwork.Accepted : false,
                    Design = string.IsNullOrWhiteSpace(request.Design) ? "artwork" : request.Design,
                    PatternJson = request.PatternJson ?? "",
                    // Only overwrite the artwork-level optional prompt on first generation;
                    // when regenerating a placement, preserve the existing artwork-level prompt
                    OptionalPrompt = isFirstGeneration ? request.OptionalPrompt : (existingArtwork?.OptionalPrompt ?? ""),
                };
                var created = await _projectCollectionArtworkRepository.UpsertAsync(collectionArtwork);

                // Delete any existing placement variants for this artwork only on first generation (regeneration scenario)
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

                    var genQuality = "medium";
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

                                // Update or create the placement variant record with group info
                                var existingGroupPlacement = await _projectCollectionArtworkPlacementRepository.GetByArtworkIdGroupAndPositionAsync(created.Id, groupId, placement.Position ?? "");
                                if (existingGroupPlacement != null)
                                {
                                    existingGroupPlacement.Width = placement.Width;
                                    existingGroupPlacement.Height = placement.Height;
                                    existingGroupPlacement.FullSize = request.IsFullSize;
                                    existingGroupPlacement.ResponseId = genResult.ResponseId ?? "";
                                    existingGroupPlacement.Index = segIdx;
                                    await _projectCollectionArtworkPlacementRepository.UpdateAsync(existingGroupPlacement);
                                }
                                else
                                {
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

                            // Update or create the placement variant record
                            var existingPlacement = await _projectCollectionArtworkPlacementRepository.GetByArtworkIdAndIndexAsync(created.Id, i);
                            if (existingPlacement != null)
                            {
                                existingPlacement.Width = task.PlacementWidth;
                                existingPlacement.Height = task.PlacementHeight;
                                existingPlacement.FullSize = request.IsFullSize;
                                existingPlacement.ResponseId = genResult.ResponseId ?? "";
                                await _projectCollectionArtworkPlacementRepository.UpdateAsync(existingPlacement);
                            }
                            else
                            {
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
                            }

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
                await _projectCollectionArtworkRepository.SetPrintifyImageIdAsync(artwork.Id, "");
                await _projectCollectionArtworkRepository.UpdateFullSizeAsync(artwork.Id, false);

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

        [HttpPost("edit-artwork")]
        public async Task<IActionResult> EditArtwork([FromBody] EditArtworkRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.ProjectId == Guid.Empty || request.CollectionId == Guid.Empty || request.ItemId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Project ID, Collection ID, and Item ID are required." });

            if (!request.Rotate180 && !request.FlipHorizontal && !request.FlipVertical)
                return Json(new ApiResponse { success = false, message = "At least one edit operation must be specified." });

            try
            {
                var project = await _projectRepository.GetByIdAsync(request.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                var artwork = await _projectCollectionArtworkRepository.GetByCollectionAndItemIdAsync(request.CollectionId, request.ItemId);
                if (artwork == null || !artwork.Active)
                    return Json(new ApiResponse { success = false, message = "No artwork found." });

                // Parse opacity settings
                OpacitySettings? opacitySettings = null;
                if (artwork.Opacity)
                {
                    var itemArtworkList = await _projectItemArtworkRepository.GetByItemIdAsync(request.ItemId);
                    var itemArtwork = itemArtworkList.FirstOrDefault();
                    opacitySettings = _opacityService.ParseOpacityJson(itemArtwork?.OpacityJson);
                }

                // Helper to apply all requested transformations
                async Task<byte[]> ApplyTransformsAsync(byte[] bytes)
                {
                    if (request.Rotate180)
                        bytes = await _imageService.Flip180Async(bytes);
                    if (request.FlipHorizontal)
                        bytes = await _imageService.MirrorYAsync(bytes);
                    if (request.FlipVertical)
                        bytes = await _imageService.MirrorXAsync(bytes);
                    return bytes;
                }

                // Get placement records
                var placements = await _projectCollectionArtworkPlacementRepository.GetByArtworkIdAsync(artwork.Id);
                var placementList = placements.ToList();

                if (request.GroupId.HasValue)
                {
                    var groupPlacements = placementList.Where(p => p.GroupId == request.GroupId.Value).ToList();
                    if (groupPlacements.Count == 0)
                        return Json(new ApiResponse { success = false, message = "No placements found for this group." });

                    // --- Seamless placement group: transform the full image, then re-cut ---
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

                    // Apply transforms to the full image
                    var transformedBytes = await ApplyTransformsAsync(fullImageBytes);

                    // Save the transformed full image (both JPG and PNG if opacity)
                    if (artwork.Opacity && opacitySettings != null && opacitySettings.ChromaKeys.Count > 0)
                    {
                        // transformedBytes is PNG — save to both fullsize and original
                        await _imageService.SaveProjectCollectionArtworkFullSizePngAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, transformedBytes);
                        await _imageService.SaveProjectCollectionArtworkPngAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, transformedBytes);

                        // Also re-apply chroma key to get the JPG version
                        var jpgBytes = await _opacityService.ApplyChromaKeysAsync(transformedBytes, opacitySettings);
                        if (opacitySettings.Overlay != null && !string.IsNullOrWhiteSpace(opacitySettings.Overlay.Color))
                            jpgBytes = await _opacityService.ApplyOverlayAsync(jpgBytes, opacitySettings.Overlay.Color);
                        var (bgBytes, bgColor) = await GetBackgroundBytesAsync(request.ProjectId, request.CollectionId, opacitySettings);
                        var jpgWithBgBytes = await _opacityService.CompositeOverBackgroundAsync(jpgBytes, bgBytes, bgColor);
                        await _imageService.SaveProjectCollectionArtworkJpgWithBgAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, jpgWithBgBytes);

                        // Save the JPG base image too (transform the original JPG separately) — both fullsize and original
                        var fullJpg = await _imageService.GetProjectCollectionArtworkFullSizeAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id);
                        if (fullJpg != null && fullJpg.Length > 0)
                        {
                            var transformedFullJpg = await ApplyTransformsAsync(fullJpg);
                            await _imageService.SaveProjectCollectionArtworkFullSizeAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, transformedFullJpg);
                        }
                        var origJpg = await _imageService.GetProjectCollectionArtworkImageAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id);
                        if (origJpg != null && origJpg.Length > 0)
                        {
                            var transformedOrigJpg = await ApplyTransformsAsync(origJpg);
                            await _imageService.SaveProjectCollectionArtworkAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, transformedOrigJpg);
                        }
                    }
                    else
                    {
                        // Non-opacity: transformedBytes is JPG — save to both fullsize and original
                        await _imageService.SaveProjectCollectionArtworkFullSizeAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, transformedBytes);
                        await _imageService.SaveProjectCollectionArtworkAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, transformedBytes);
                    }

                    // Re-cut the transformed image into segments for this group
                    var groupId = request.GroupId.Value;
                    var orderedPlacements = groupPlacements.OrderBy(p => p.Index).ToList();
                    var segmentHeights = orderedPlacements.Select(p => p.Height).ToList();
                    var segments = await _imageService.CutImageVerticalAsync(transformedBytes, segmentHeights);

                    for (var segIdx = 0; segIdx < segments.Count && segIdx < orderedPlacements.Count; segIdx++)
                    {
                        var placement = orderedPlacements[segIdx];
                        var segBytes = segments[segIdx];
                        var position = placement.Position ?? "";

                        if (artwork.Opacity && opacitySettings != null && opacitySettings.ChromaKeys.Count > 0)
                        {
                            await _imageService.SaveProjectCollectionArtworkGroupImageFullSizePngAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, groupId, position, segBytes);
                            await _imageService.SaveProjectCollectionArtworkGroupImagePngAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, groupId, position, segBytes);
                        }
                        else
                        {
                            await _imageService.SaveProjectCollectionArtworkGroupImageFullSizeAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, groupId, position, segBytes);
                            await _imageService.SaveProjectCollectionArtworkGroupImageAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, groupId, position, segBytes);
                        }
                    }
                }
                else if (request.PlacementIndex.HasValue)
                {
                    // --- Specific non-group placement: only transform that placement ---
                    var pIdx = request.PlacementIndex.Value;

                    // Transform JPG (both original and fullsize)
                    var pJpgFull = await _imageService.GetProjectCollectionArtworkPlacementFullSizeAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, pIdx);
                    var pJpgOrig = await _imageService.GetProjectCollectionArtworkPlacementImageAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, pIdx);

                    if ((pJpgFull == null || pJpgFull.Length == 0) && (pJpgOrig == null || pJpgOrig.Length == 0))
                        return Json(new ApiResponse { success = false, message = "Placement image not found." });

                    if (pJpgFull != null && pJpgFull.Length > 0)
                    {
                        var transformed = await ApplyTransformsAsync(pJpgFull);
                        await _imageService.SaveProjectCollectionArtworkPlacementFullSizeAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, pIdx, transformed);
                    }
                    if (pJpgOrig != null && pJpgOrig.Length > 0)
                    {
                        var transformed = await ApplyTransformsAsync(pJpgOrig);
                        await _imageService.SaveProjectCollectionArtworkPlacementAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, pIdx, transformed);
                    }

                    // Transform PNG (both original and fullsize) if opacity
                    if (artwork.Opacity && opacitySettings != null && opacitySettings.ChromaKeys.Count > 0)
                    {
                        var pPngFull = await _imageService.GetProjectCollectionArtworkPlacementFullSizePngAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, pIdx);
                        var pPngOrig = await _imageService.GetProjectCollectionArtworkPlacementPngAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, pIdx);

                        if (pPngFull != null && pPngFull.Length > 0)
                        {
                            var transformed = await ApplyTransformsAsync(pPngFull);
                            await _imageService.SaveProjectCollectionArtworkPlacementFullSizePngAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, pIdx, transformed);
                        }
                        if (pPngOrig != null && pPngOrig.Length > 0)
                        {
                            var transformed = await ApplyTransformsAsync(pPngOrig);
                            await _imageService.SaveProjectCollectionArtworkPlacementPngAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, pIdx, transformed);
                        }
                    }

                    // Regenerate thumbnail for this placement
                    await _imageService.GenerateProjectCollectionArtworkPlacementThumbAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, pIdx);

                    // Clear printify image ID for just this placement
                    var placement = placementList.FirstOrDefault(p => p.Index == pIdx && !p.GroupId.HasValue);
                    if (placement != null)
                        await _projectCollectionArtworkPlacementRepository.SetPrintifyImageIdAsync(placement.Id, "");
                }
                else
                {
                    // --- Single artwork (no placements): transform the base artwork image ---
                    // Transform JPG (both original and fullsize)
                    var jpgFull = await _imageService.GetProjectCollectionArtworkFullSizeAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id);
                    var jpgOrig = await _imageService.GetProjectCollectionArtworkImageAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id);

                    if ((jpgFull == null || jpgFull.Length == 0) && (jpgOrig == null || jpgOrig.Length == 0))
                        return Json(new ApiResponse { success = false, message = "Generated artwork image not found." });

                    if (jpgFull != null && jpgFull.Length > 0)
                    {
                        var transformed = await ApplyTransformsAsync(jpgFull);
                        await _imageService.SaveProjectCollectionArtworkFullSizeAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, transformed);
                    }
                    if (jpgOrig != null && jpgOrig.Length > 0)
                    {
                        var transformed = await ApplyTransformsAsync(jpgOrig);
                        await _imageService.SaveProjectCollectionArtworkAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, transformed);
                    }

                    // If opacity, also transform the PNG (both original and fullsize)
                    if (artwork.Opacity && opacitySettings != null && opacitySettings.ChromaKeys.Count > 0)
                    {
                        var pngFull = await _imageService.GetProjectCollectionArtworkFullSizePngAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id);
                        var pngOrig = await _imageService.GetProjectCollectionArtworkPngAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id);

                        byte[]? transformedPng = null;
                        if (pngFull != null && pngFull.Length > 0)
                        {
                            transformedPng = await ApplyTransformsAsync(pngFull);
                            await _imageService.SaveProjectCollectionArtworkFullSizePngAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, transformedPng);
                        }
                        if (pngOrig != null && pngOrig.Length > 0)
                        {
                            var transformedOrigPng = await ApplyTransformsAsync(pngOrig);
                            await _imageService.SaveProjectCollectionArtworkPngAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, transformedOrigPng);
                            if (transformedPng == null) transformedPng = transformedOrigPng;
                        }

                        if (transformedPng != null)
                        {
                            // Re-generate JPG with background
                            var (bgBytes, bgColor) = await GetBackgroundBytesAsync(request.ProjectId, request.CollectionId, opacitySettings);
                            var jpgWithBgBytes = await _opacityService.CompositeOverBackgroundAsync(transformedPng, bgBytes, bgColor);
                            await _imageService.SaveProjectCollectionArtworkJpgWithBgAsync(request.ProjectId, request.CollectionId, request.ItemId, artwork.Id, jpgWithBgBytes);
                        }
                    }

                    await _projectCollectionArtworkRepository.SetPrintifyImageIdAsync(artwork.Id, "");
                }

                // For placement groups, reset FullSize since segments are re-cut from the transformed image and need upscaling again
                if (request.GroupId.HasValue)
                {
                    var groupPlacements = placementList.Where(p => p.GroupId == request.GroupId.Value).ToList();
                    foreach (var p in groupPlacements)
                        await _projectCollectionArtworkPlacementRepository.SetFullSizeAsync(p.Id, false);
                }

                // Regenerate thumbnails
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

                // Get placement variants for this artwork
                var placements = await _projectCollectionArtworkPlacementRepository.GetByArtworkIdAsync(artwork.Id);
                var placementList = placements.ToList();

                // When targeting a specific placement or group, filter to only that one
                var isTargeted = request.PlacementIndex.HasValue || request.GroupId.HasValue;

                // When forcing re-upscale, reset full-size flags and Printify image IDs for the targeted placements
                if (request.Force)
                {
                    var placementsToReset = isTargeted
                        ? placementList.Where(p =>
                            (request.GroupId.HasValue && p.GroupId == request.GroupId) ||
                            (request.PlacementIndex.HasValue && !p.GroupId.HasValue && p.Index == request.PlacementIndex.Value))
                        : placementList;
                    foreach (var p in placementsToReset)
                    {
                        await _projectCollectionArtworkPlacementRepository.SetFullSizeAsync(p.Id, false);
                        await _projectCollectionArtworkPlacementRepository.SetPrintifyImageIdAsync(p.Id, "");
                    }
                    if (!isTargeted || placementList.Count == 0)
                    {
                        artwork.FullSize = false;
                        await _projectCollectionArtworkRepository.SetPrintifyImageIdAsync(artwork.Id, "");
                    }
                }

                // Determine which placements need upscaling (FullSize == false)
                // For group artworks or single artworks with no placements, use artwork-level FullSize
                var pendingPlacements = placementList.Where(p => !p.FullSize).ToList();
                if (isTargeted)
                {
                    pendingPlacements = pendingPlacements.Where(p =>
                        (request.GroupId.HasValue && p.GroupId == request.GroupId) ||
                        (request.PlacementIndex.HasValue && !p.GroupId.HasValue && p.Index == request.PlacementIndex.Value)).ToList();
                }
                var needsArtworkUpscale = !isTargeted && artwork.FullSize == false && placementList.Count == 0;
                if (pendingPlacements.Count == 0 && !needsArtworkUpscale && !request.Force)
                    return Json(new ApiResponse { success = true, data = artwork });

                // Determine token cost: 2 tokens per pending variant (or 2 for single artwork)
                var variantCount = Math.Max(1, request.Force ? (isTargeted ? pendingPlacements.Count : placementList.Count) : (pendingPlacements.Count > 0 ? pendingPlacements.Count : 1));
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
                    if (request.GroupId.HasValue)
                        groupPlacements = groupPlacements.Where(p => p.GroupId == request.GroupId.Value).ToList();
                    // Only upscale standard placements that haven't been upscaled yet (unless Force)
                    var standardPlacements = placementList.Where(p => !p.GroupId.HasValue && (request.Force || !p.FullSize)).ToList();
                    if (request.PlacementIndex.HasValue)
                        standardPlacements = standardPlacements.Where(p => p.Index == request.PlacementIndex.Value).ToList();

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
                    // Only process groups that have at least one placement needing upscaling (unless Force)
                    var groups = groupPlacements
                        .GroupBy(p => p.GroupId!.Value)
                        .Where(g => request.Force || g.Any(p => !p.FullSize));
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

                // Set artwork FullSize = true only when ALL placements are upscaled (or no placements exist)
                var allPlacements = await _projectCollectionArtworkPlacementRepository.GetByArtworkIdAsync(artwork.Id);
                var allPlacementList = allPlacements.ToList();
                artwork.FullSize = allPlacementList.Count == 0 || allPlacementList.All(p => p.FullSize);
                await _projectCollectionArtworkRepository.UpdateFullSizeAsync(artwork.Id, artwork.FullSize);

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

        [HttpPost("update-collection-artwork-optional-prompt")]
        public async Task<IActionResult> UpdateCollectionArtworkOptionalPrompt([FromBody] UpdateCollectionArtworkOptionalPromptRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.CollectionId == Guid.Empty || request.ItemId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Collection ID and Item ID are required." });

            try
            {
                var collection = await _projectCollectionRepository.GetByIdAsync(request.CollectionId);
                if (collection == null || collection.Status != 1)
                    return Json(new ApiResponse { success = false, message = "Collection not found." });

                var project = await _projectRepository.GetByIdAsync(collection.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                var artwork = await _projectCollectionArtworkRepository.GetByCollectionAndItemIdAsync(request.CollectionId, request.ItemId);
                if (artwork == null)
                    return Json(new ApiResponse { success = false, message = "Collection artwork not found." });

                await _projectCollectionArtworkRepository.UpdateOptionalPromptAsync(request.CollectionId, request.ItemId, request.OptionalPrompt);

                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("update-placement-optional-prompt")]
        public async Task<IActionResult> UpdatePlacementOptionalPrompt([FromBody] UpdatePlacementOptionalPromptRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.CollectionId == Guid.Empty || request.ItemId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Collection ID and Item ID are required." });

            try
            {
                var collection = await _projectCollectionRepository.GetByIdAsync(request.CollectionId);
                if (collection == null || collection.Status != 1)
                    return Json(new ApiResponse { success = false, message = "Collection not found." });

                var project = await _projectRepository.GetByIdAsync(collection.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                var artwork = await _projectCollectionArtworkRepository.GetByCollectionAndItemIdAsync(request.CollectionId, request.ItemId);
                if (artwork == null)
                    return Json(new ApiResponse { success = false, message = "Collection artwork not found." });

                if (request.GroupId.HasValue)
                {
                    // Save to all placements in the group
                    await _projectCollectionArtworkPlacementRepository.SetOptionalPromptByGroupAsync(artwork.Id, request.GroupId.Value, request.OptionalPrompt ?? "");
                }
                else
                {
                    // Save to a single placement by index
                    var placement = await _projectCollectionArtworkPlacementRepository.GetByArtworkIdAndIndexAsync(artwork.Id, request.PlacementIndex);
                    if (placement == null)
                        return Json(new ApiResponse { success = false, message = "Placement not found." });
                    await _projectCollectionArtworkPlacementRepository.SetOptionalPromptAsync(placement.Id, request.OptionalPrompt ?? "");
                }

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
                    request.CollectionId, request.ProjectBlueprintId ?? Guid.Empty, request.ProductImageId);
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
                        request.CollectionId, combo.ProjectBlueprintId ?? Guid.Empty, combo.ProductImageId);
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
                            request.CollectionId, img.ProjectBlueprintId ?? Guid.Empty, img.ProductImageId);
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
                        await _projectCollectionProductImageRepository.UpdateActiveAsync(existing.Id, existing.Active);
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
        public async Task<IActionResult> GetCollectionArtworkGroupImage(Guid collectionId, Guid itemId, Guid artworkId, Guid groupId, string position, [FromQuery] bool fullSize = false, [FromQuery] bool png = false, [FromQuery] bool thumb = false)
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

                if (thumb)
                    bytes = await _imageService.GenerateThumbnailAsync(bytes);

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
                        var existingArt = existingArtworkByItem.TryGetValue(aiItem.Id, out var existingList) ? existingList.FirstOrDefault() : null;
                        var plan = await _artworkGenerationPlanService.BuildPlanAsync(request.ProjectId, request.CollectionId ?? Guid.Empty, aiItem.Id, resolutionTier: 2, design: existingArt?.Design ?? "artwork");
                        var itemTokens = 0m;

                        // Check if the plan's total placements matches the stored TotalPlacements on existing artwork
                        var itemNeedsRegeneration = existingArt != null && existingArt.TotalPlacements != plan.TotalPlacements;

                        foreach (var task in plan.Tasks)
                        {
                            // Calculate tokens using the actual token formula (same as real generation)
                            int taskTokensInt;
                            if (tokenizer != null)
                            {
                                var tokenCalc = tokenizer.CalculateTokens(plan.FinalPrompt, task.Width, task.Height, "medium", null, "auto", tokenCost);
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

                var bp = request.ProjectBlueprintId.HasValue && request.ProjectBlueprintId.Value != Guid.Empty
                    ? await _projectBlueprintRepository.GetByIdAsync(request.ProjectBlueprintId.Value)
                    : null;
                if (request.ProjectBlueprintId.HasValue && request.ProjectBlueprintId.Value != Guid.Empty && (bp == null || bp.ProjectId != request.ProjectId))
                    return Json(new ApiResponse { success = false, message = "Blueprint not found." });

                var printifyBlueprintId = bp?.BlueprintId ?? 0;

                var printifyBlueprint = printifyBlueprintId > 0
                    ? await _printifyBlueprintRepository.GetByBlueprintIdAsync(printifyBlueprintId)
                    : null;

                var collectionArtwork = (await _projectCollectionArtworkRepository.GetByCollectionIdAsync(request.CollectionId)).ToList();

                // Collect only the first placement artwork for this product (skip for custom images with no blueprint)
                var placementArtworks = new List<(string PlacementName, Guid ItemId, Guid ArtworkId, byte[] ImageBytes)>();
                if (bp != null)
                {
                    try
                    {
                        var placementArr = System.Text.Json.JsonSerializer.Deserialize<List<PlacementDto>>(bp.PlacementJson ?? "[]");
                        if (placementArr != null && placementArr.Count > 0)
                        {
                            var firstPlacement = placementArr[0];
                            var pItemId = firstPlacement.GetItemId();
                            if (pItemId != Guid.Empty)
                            {
                                var pArtwork = collectionArtwork.FirstOrDefault(a => a.ItemId == pItemId && a.Active);
                                if (pArtwork != null)
                                {
                                    var (pw, ph) = firstPlacement.GetDimensions();
                                    var placementVariants = (await _projectCollectionArtworkPlacementRepository.GetByArtworkIdAsync(pArtwork.Id)).ToList();

                                    var pImgBytes = await GetPlacementSpecificArtworkAsync(
                                        request.ProjectId, request.CollectionId, pItemId, pArtwork.Id, pArtwork.Opacity,
                                        firstPlacement.Position ?? "", pw, ph, placementVariants);

                                    if (pImgBytes != null && pImgBytes.Length > 0)
                                    {
                                        placementArtworks.Add((firstPlacement.Position ?? "", pItemId, pArtwork.Id, pImgBytes));
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }

                if (placementArtworks.Count == 0 && bp != null)
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

                if (request.ModelId == null || request.ModelId <= 0)
                    return Json(new ApiResponse { success = false, message = "Model ID is required." });

                var genModel = await _imageGenerationModelRepository.GetByIdAsync(request.ModelId.Value);
                if (genModel == null)
                    return Json(new ApiResponse { success = false, message = "Image model not found in database." });

                var tokenCost = _tokenCostOptions.Cost > 0 ? _tokenCostOptions.Cost : 0.01m;

                IImageTokens? tokenizer = null;
                var imageGen = _imageGenerations.FirstOrDefault(g => g.ModelKey.Equals(genModel.ModelKey, StringComparison.OrdinalIgnoreCase));
                if (imageGen != null)
                    tokenizer = imageGen.CreateTokenizer(genModel);

                var inputImages = new List<(int width, int height)>();
                var referenceImageDtos = new List<ReferenceImageDto>();

                if (printifyBlueprintId > 0 || (request.ProjectBlueprintId == null && request.MockupImageIds != null && request.MockupImageIds.Count > 0))
                {
                    var printifyProducts = await _printifyProductRepository.GetByCollectionIdAsync(request.CollectionId);
                    var collectionProducts = await _productRepository.GetByCollectionIdAsync(request.CollectionId);
                    var productByBlueprintId = collectionProducts.ToDictionary(p => p.ProjectBlueprintId);
                    var printifyProduct = printifyProducts.FirstOrDefault(p => request.ProjectBlueprintId.HasValue && productByBlueprintId.TryGetValue(request.ProjectBlueprintId.Value, out var prod) && prod.Id == p.ProductId);

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

                        // For custom images, also search mockups from other products in the collection
                        if (request.ProjectBlueprintId == null && selectedMockups.Count < (request.MockupImageIds?.Count ?? 0))
                        {
                            var foundIds = new HashSet<Guid>(selectedMockups.Select(m => m.Id));
                            var missingIds = (request.MockupImageIds ?? new List<Guid>()).Where(id => !foundIds.Contains(id)).ToList();
                            if (missingIds.Count > 0)
                            {
                                foreach (var otherPp in printifyProducts.Where(p => p.Id != printifyProduct.Id))
                                {
                                    var otherMockups = (await _mockupRepository.GetByPrintifyProductIdAsync(otherPp.Id))
                                        .Where(m => missingIds.Contains(m.Id));
                                    selectedMockups.AddRange(otherMockups);
                                    foreach (var om in otherMockups)
                                        foundIds.Add(om.Id);
                                    if (foundIds.Count >= (request.MockupImageIds?.Count ?? 0)) break;
                                }
                            }
                        }

                        foreach (var mockup in selectedMockups)
                        {
                            var imgBytes = await _imageService.GetProjectCollectionMockupAsync(
                                request.ProjectId, request.CollectionId, mockup.Id);
                            var dims = await _imageService.GetImageDimensionsAsync(imgBytes);
                            var w = dims?.width ?? 1024;
                            var h = dims?.height ?? 1024;
                            inputImages.Add((w, h));
                            referenceImageDtos.Add(new ReferenceImageDto
                            {
                                Name = $"Mockup ({mockup.Position})",
                                Type = "mockup",
                                Width = w,
                                Height = h
                            });
                            mockupImageCount++;
                        }
                    }
                    else if (request.ProjectBlueprintId == null)
                    {
                        // Custom image with no specific product — search all mockups in the collection
                        foreach (var pp in printifyProducts)
                        {
                            var mockups = (await _mockupRepository.GetByPrintifyProductIdAsync(pp.Id))
                                .Where(m => request.MockupImageIds != null && request.MockupImageIds.Contains(m.Id));
                            foreach (var mockup in mockups)
                            {
                                var imgBytes = await _imageService.GetProjectCollectionMockupAsync(
                                    request.ProjectId, request.CollectionId, mockup.Id);
                                var dims = await _imageService.GetImageDimensionsAsync(imgBytes);
                                var w = dims?.width ?? 1024;
                                var h = dims?.height ?? 1024;
                                inputImages.Add((w, h));
                                referenceImageDtos.Add(new ReferenceImageDto
                                {
                                    Name = $"Mockup ({mockup.Position})",
                                    Type = "mockup",
                                    Width = w,
                                    Height = h
                                });
                                mockupImageCount++;
                            }
                        }
                    }

                    // Fallback: if no mockup images were found, use the Printify blueprint image for the variant color
                    if (mockupImageCount == 0 && printifyBlueprintId > 0 && !string.IsNullOrWhiteSpace(request.VariantColor))
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
                                    var w = dims?.width ?? 1024;
                                    var h = dims?.height ?? 1024;
                                    inputImages.Add((w, h));
                                    referenceImageDtos.Add(new ReferenceImageDto
                                    {
                                        Name = $"Blueprint Image {matchingImage.ImageIndex}",
                                        Type = "blueprint",
                                        Width = w,
                                        Height = h
                                    });
                                }
                            }
                        }
                    }
                }

                // Pre-load item titles for artwork placements
                var artworkItemIds = placementArtworks.Select(pa => pa.ItemId).Distinct().ToList();
                var itemTitleMap = new Dictionary<Guid, string>();
                foreach (var iid in artworkItemIds)
                {
                    var item = await _projectItemRepository.GetByIdAsync(iid);
                    itemTitleMap[iid] = item?.Title ?? "Artwork";
                }

                foreach (var pa in placementArtworks)
                {
                    var dims = await _imageService.GetImageDimensionsAsync(pa.ImageBytes);
                    var w = dims?.width ?? 1024;
                    var h = dims?.height ?? 1024;
                    inputImages.Add((w, h));
                    referenceImageDtos.Add(new ReferenceImageDto
                    {
                        Name = itemTitleMap.TryGetValue(pa.ItemId, out var title) ? title : "Artwork",
                        Type = "artwork",
                        Width = w,
                        Height = h
                    });
                }

                int textInputTokens = 0;
                int imageInputTokens = 0;
                int imageOutputTokens = 0;
                decimal estimatedCostUSD = 0m;
                int totalTokens = 0;

                if (tokenizer != null)
                {
                    var tokenResult = tokenizer.CalculateTokens(finalPrompt, 1024, 1024, "medium", inputImages, "auto", tokenCost);
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
                        Width = 1024,
                        Height = 1024,
                        NeedsUpscale = true,
                        Tokens = totalTokens,
                        Placements = bp != null ? placementArtworks.Select(pa => new EstimatePlacementDto
                        {
                            BlueprintId = bp.BlueprintId,
                            BlueprintName = bp.Name ?? "",
                            Position = pa.PlacementName,
                            Width = 0,
                            Height = 0
                        }).ToList() : new List<EstimatePlacementDto>(),
                        ReferenceImages = referenceImageDtos
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
                // Try to match by position first (works for group placements which have Position set)
                if (!string.IsNullOrWhiteSpace(position))
                {
                    var byPosition = placementVariants.FirstOrDefault(v =>
                        !string.IsNullOrWhiteSpace(v.Position) &&
                        string.Equals(v.Position, position, StringComparison.OrdinalIgnoreCase));

                    if (byPosition != null)
                    {
                        var img = await LoadPlacementImageAsync(projectId, collectionId, itemId, artworkId, opacity, byPosition);
                        if (img != null && img.Length > 0) return img;
                    }
                }

                // Fall back to aspect ratio matching for non-group placements (which don't have Position set)
                if (placementWidth > 0 && placementHeight > 0)
                {
                    var placementRatio = (double)placementWidth / placementHeight;
                    var byRatio = placementVariants.FirstOrDefault(v =>
                        !v.GroupId.HasValue &&
                        v.Width > 0 && v.Height > 0 &&
                        Math.Abs((double)v.Width / v.Height - placementRatio) < 0.01);

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

                var bp = request.ProjectBlueprintId.HasValue && request.ProjectBlueprintId.Value != Guid.Empty
                    ? await _projectBlueprintRepository.GetByIdAsync(request.ProjectBlueprintId.Value)
                    : null;
                if (request.ProjectBlueprintId.HasValue && request.ProjectBlueprintId.Value != Guid.Empty && (bp == null || bp.ProjectId != request.ProjectId))
                    return Json(new ApiResponse { success = false, message = "Blueprint not found." });

                var printifyBlueprintId = bp?.BlueprintId ?? 0;

                var printifyBlueprint = printifyBlueprintId > 0
                    ? await _printifyBlueprintRepository.GetByBlueprintIdAsync(printifyBlueprintId)
                    : null;

                var collectionArtwork = (await _projectCollectionArtworkRepository.GetByCollectionIdAsync(request.CollectionId)).ToList();

                // Collect only the first placement artwork for this product (skip for custom images with no blueprint)
                var placementArtworks = new List<(string PlacementName, Guid ItemId, Guid ArtworkId, byte[] ImageBytes)>();
                if (bp != null)
                {
                    try
                    {
                        var placementArr = System.Text.Json.JsonSerializer.Deserialize<List<PlacementDto>>(bp.PlacementJson ?? "[]");
                        if (placementArr != null && placementArr.Count > 0)
                        {
                            var firstPlacement = placementArr[0];
                            var pItemId = firstPlacement.GetItemId();
                            if (pItemId != Guid.Empty)
                            {
                                var pArtwork = collectionArtwork.FirstOrDefault(a => a.ItemId == pItemId && a.Active);
                                if (pArtwork != null)
                                {
                                    var (pw, ph) = firstPlacement.GetDimensions();
                                    var placementVariants = (await _projectCollectionArtworkPlacementRepository.GetByArtworkIdAsync(pArtwork.Id)).ToList();

                                    var pImgBytes = await GetPlacementSpecificArtworkAsync(
                                        request.ProjectId, request.CollectionId, pItemId, pArtwork.Id, pArtwork.Opacity,
                                        firstPlacement.Position ?? "", pw, ph, placementVariants);

                                    if (pImgBytes != null && pImgBytes.Length > 0)
                                    {
                                        placementArtworks.Add((firstPlacement.Position ?? "", pItemId, pArtwork.Id, pImgBytes));
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }

                if (placementArtworks.Count == 0 && bp != null)
                    return Json(new ApiResponse { success = false, message = "No accepted artwork found for any placement." });

                var promptBuilder = new StringBuilder();
                promptBuilder.AppendLine("Apply the following artwork designs onto the product shown in the reference image.");
                promptBuilder.AppendLine("Place the product in a realistic, appealing scenario as described below.");

                if (request.ModelId == null || request.ModelId <= 0)
                    return Json(new ApiResponse { success = false, message = "Model ID is required." });

                var genModel = await _imageGenerationModelRepository.GetByIdAsync(request.ModelId.Value);
                if (genModel == null)
                    return Json(new ApiResponse { success = false, message = "Image model not found in database." });

                var inputImages = new List<byte[]>();
                var inputImageRefs = new List<object>();

                if (printifyBlueprintId > 0 || (request.ProjectBlueprintId == null && request.MockupImageIds != null && request.MockupImageIds.Count > 0))
                {
                    var printifyProducts = await _printifyProductRepository.GetByCollectionIdAsync(request.CollectionId);
                    var collectionProducts = await _productRepository.GetByCollectionIdAsync(request.CollectionId);
                    var productByBlueprintId = collectionProducts.ToDictionary(p => p.ProjectBlueprintId);
                    var printifyProduct = printifyProducts.FirstOrDefault(p => request.ProjectBlueprintId.HasValue && productByBlueprintId.TryGetValue(request.ProjectBlueprintId.Value, out var prod) && prod.Id == p.ProductId);

                    var printifyImageCount = 0;
                    var printifyImagePositions = new List<string>();

                    if (printifyProduct != null)
                    {
                        var mockups = (await _mockupRepository.GetByPrintifyProductIdAsync(printifyProduct.Id)).ToList();
                        var selectedMockups = mockups
                            .Where(m => request.MockupImageIds != null && request.MockupImageIds.Contains(m.Id))
                            .ToList();

                        // For custom images, also search mockups from other products in the collection
                        if (request.ProjectBlueprintId == null && selectedMockups.Count < (request.MockupImageIds?.Count ?? 0))
                        {
                            var foundIds = new HashSet<Guid>(selectedMockups.Select(m => m.Id));
                            var missingIds = (request.MockupImageIds ?? new List<Guid>()).Where(id => !foundIds.Contains(id)).ToList();
                            if (missingIds.Count > 0)
                            {
                                foreach (var otherPp in printifyProducts.Where(p => p.Id != printifyProduct.Id))
                                {
                                    var otherMockups = (await _mockupRepository.GetByPrintifyProductIdAsync(otherPp.Id))
                                        .Where(m => missingIds.Contains(m.Id));
                                    selectedMockups.AddRange(otherMockups);
                                    foreach (var om in otherMockups)
                                        foundIds.Add(om.Id);
                                    if (foundIds.Count >= (request.MockupImageIds?.Count ?? 0)) break;
                                }
                            }
                        }

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
                    else if (request.ProjectBlueprintId == null)
                    {
                        // Custom image with no specific product — search all mockups in the collection
                        foreach (var pp in printifyProducts)
                        {
                            var mockups = (await _mockupRepository.GetByPrintifyProductIdAsync(pp.Id))
                                .Where(m => request.MockupImageIds != null && request.MockupImageIds.Contains(m.Id));
                            foreach (var mockup in mockups)
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
                    }

                    // Fallback: if no mockup images were found, use the ProjectBlueprintProductImages reference image
                    if (printifyImageCount == 0 && printifyBlueprintId > 0 && !string.IsNullOrWhiteSpace(request.VariantColor))
                    {
                        var bpImages = (await _printifyBlueprintImageRepository.GetByBlueprintIdAsync(printifyBlueprintId)).ToList();
                        if (bpImages.Count > 0)
                        {
                            var blueprintProductImages = (await _projectBlueprintProductImageRepository.GetByProjectBlueprintIdAsync(request.ProjectBlueprintId ?? Guid.Empty)).ToList();
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
                        var productName = bp?.Name ?? "the product(s)";
                        promptBuilder.AppendLine($"The next {printifyImageCount} image(s) are mockup reference images for {productName}:");
                        for (var i = 0; i < printifyImageCount; i++)
                        {
                            var posStr = printifyImagePositions[i];
                            promptBuilder.AppendLine($"- Image {i + 1}: Mockup product image ({posStr} view). Isolate the product from the person and background in this reference image to use in the final output.");
                        }

                        promptBuilder.AppendLine();
                        promptBuilder.AppendLine("The artwork designs shown on the product in the reference mockup images must remain in the exact same position and at the exact same size as they appear in those mockups. Do not move, resize, reposition, or alter the artwork placement in any way.");
                    }
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

                // Look up existing record — by Id if provided (custom images), otherwise by ProductImageId
                ProjectCollectionProductImage? existing = null;
                if (request.Id.HasValue && request.Id.Value != Guid.Empty)
                {
                    existing = await _projectCollectionProductImageRepository.GetByIdAsync(request.Id.Value);
                    if (existing != null && existing.CollectionId != request.CollectionId)
                        existing = null;
                }
                if (existing == null && request.ProductImageId != Guid.Empty)
                {
                    existing = await _projectCollectionProductImageRepository.GetByCollectionBlueprintProductImageIdAsync(
                        request.CollectionId, request.ProjectBlueprintId ?? Guid.Empty, request.ProductImageId, activeOnly: false);
                }

                // Determine whether to include artwork reference images
                var includeArtworkRef = request.IncludeArtworkRef ?? existing?.IncludeArtworkRef ?? true;

                // Add artwork images after mockup images, then describe placements
                var artworkStartIndex = inputImages.Count + 1;
                if (includeArtworkRef && placementArtworks.Count > 0)
                {
                    promptBuilder.AppendLine();
                    promptBuilder.AppendLine($"The next {placementArtworks.Count} image(s) are the artwork designs to be placed on the product:");
                }
                if (includeArtworkRef)
                {
                    for (var i = 0; i < placementArtworks.Count; i++)
                    {
                        inputImages.Add(placementArtworks[i].ImageBytes);
                        inputImageRefs.Add(new { type = "artwork", id = placementArtworks[i].ArtworkId.ToString() });
                        promptBuilder.AppendLine($"- Image {artworkStartIndex + i}: Artwork design for the {placementArtworks[i].PlacementName} of the product.");
                    }
                }

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
                    Width = 1024,
                    Height = 1024,
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
                var productTokenCalc = productTokenizer.CalculateTokens(finalPrompt, 1024, 1024, "medium", productImageInputDimensions, "auto", productTokenCost);
                var productTokensToUse = productTokenCalc.PlatformTokens;

                if (!await _aiTokenService.UseTokensAsync(userId, productTokensToUse))
                    return Json(new ApiResponse { success = false, message = "Not enough tokens to generate the product image. Please purchase more tokens before continuing." });

                var genResult = await imageGen.GenerateAsync(genRequest);

                // Upscale to 2K and resize down to 1536x1536
                var finalImageBytes = genResult.ImageBytes;
                try
                {
                    var upscaledBytes = await _imageUpscaler.UpscaleAsync(genResult.ImageBytes, 2);
                    var upscaledDims = await _imageService.GetImageDimensionsAsync(upscaledBytes);
                    if (upscaledDims.HasValue && (upscaledDims.Value.width > 1536 || upscaledDims.Value.height > 1536))
                    {
                        finalImageBytes = await _imageService.ResizeToWidthAsync(upscaledBytes, 1536);
                    }
                    else
                    {
                        finalImageBytes = upscaledBytes;
                    }
                }
                catch
                {
                    // If upscaling fails, use the original generated image
                    finalImageBytes = genResult.ImageBytes;
                }

                var productImage = new ProjectCollectionProductImage
                {
                    ProjectId = request.ProjectId,
                    CollectionId = request.CollectionId,
                    ProjectBlueprintId = request.ProjectBlueprintId,
                    ProductImageId = request.ProductImageId,
                    ImageModel = genModel.Model,
                    Prompt = request.Prompt,
                    Width = 1536,
                    Height = 1536,
                    Accepted = false,
                    ResponseId = genResult.ResponseId ?? "",
                    VariantColor = request.VariantColor ?? "",
                    Generated = true,
                    IncludeArtworkRef = includeArtworkRef
                };

                if (existing != null)
                {
                    productImage.Id = existing.Id;
                    productImage.ResponseId = genResult.ResponseId ?? existing.ResponseId;
                    productImage.SelectedMockups = existing.SelectedMockups;
                    productImage.Active = existing.Active;
                    productImage.IncludeArtworkRef = includeArtworkRef;
                    await _projectCollectionProductImageRepository.UpdateAsync(productImage);
                }
                else
                {
                    productImage = await _projectCollectionProductImageRepository.CreateAsync(productImage);
                }

                await _imageService.SaveProjectCollectionProductImageAsync(request.ProjectId, request.CollectionId, productImage.Id, finalImageBytes);
                await _imageService.GenerateProjectCollectionProductImageThumbAsync(request.ProjectId, request.CollectionId, productImage.Id);

                // Update product name if provided
                if (!string.IsNullOrWhiteSpace(request.ProductName))
                {
                    var collectionProduct = await _productRepository.GetByCollectionAndBlueprintIdAsync(request.CollectionId, request.ProjectBlueprintId ?? Guid.Empty);
                    if (collectionProduct != null && collectionProduct.Name != request.ProductName)
                    {
                        collectionProduct.Name = request.ProductName;
                        await _productRepository.UpdateNameAsync(collectionProduct.Id, collectionProduct.Name);
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
                await _projectCollectionProductImageRepository.UpdateAcceptedAsync(productImage.Id, productImage.Accepted);

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
                var collection = await _projectCollectionRepository.GetByIdAsync(collectionId);
                if (collection == null)
                    return Json(new ApiResponse { success = false, message = "Collection not found." });

                // Get all blueprint product images for this project's blueprints
                var blueprints = await _projectBlueprintRepository.GetByProjectIdAsync(collection.ProjectId);
                var blueprintIds = blueprints.Select(b => b.Id).ToList();
                var allBpProductImages = blueprintIds.Count > 0
                    ? (await _projectBlueprintProductImageRepository.GetByBlueprintIdsAsync(blueprintIds)).Where(bpi => bpi.Status == 1).ToList()
                    : new List<ProjectBlueprintProductImage>();

                // Get existing collection product images
                var images = (await _projectCollectionProductImageRepository.GetByCollectionIdAsync(collectionId)).ToList();

                // Auto-create missing ProjectCollectionProductImage records for blueprint product images
                var existingBpImageIds = new HashSet<Guid>(
                    images.Where(img => img.ProductImageId != Guid.Empty).Select(img => img.ProductImageId)
                );
                foreach (var bpi in allBpProductImages)
                {
                    if (existingBpImageIds.Contains(bpi.Id)) continue;
                    var newImg = await _projectCollectionProductImageRepository.CreateAsync(new ProjectCollectionProductImage
                    {
                        ProjectId = collection.ProjectId,
                        CollectionId = collectionId,
                        ProjectBlueprintId = bpi.ProjectBlueprintId,
                        ProductImageId = bpi.Id,
                        ImageModel = "",
                        Prompt = bpi.Prompt ?? "",
                        Width = 0,
                        Height = 0,
                        Accepted = false,
                        ResponseId = "",
                        VariantColor = bpi.VariantColor ?? "",
                        Active = true,
                        SelectedMockups = "",
                        Generated = false
                    });
                    images.Add(newImg);
                }

                // Build title map from blueprint product images
                var bpTitleMap = allBpProductImages.ToDictionary(bpi => bpi.Id, bpi => bpi.Title ?? "");

                // Fetch collection product names by blueprint
                var collectionProducts = await _productRepository.GetByCollectionIdAsync(collectionId);
                var productNameMap = collectionProducts.ToDictionary(p => p.ProjectBlueprintId, p => p.Name ?? "");

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
                        imageModel = img.ImageModel,
                        variantColor = img.VariantColor,
                        title = img.ProjectBlueprintId.HasValue && productNameMap.TryGetValue(img.ProjectBlueprintId.Value, out var pname) ? pname : "",
                        subtitle = img.ProductImageId != Guid.Empty && bpTitleMap.TryGetValue(img.ProductImageId, out var t) ? t : (img.VariantColor ?? ""),
                        selectedMockups = img.SelectedMockups,
                        generated = img.Generated,
                        includeArtworkRef = img.IncludeArtworkRef,
                        imageUrl = img.Generated ? $"/api/projects/collection/{collectionId}/product-image/{img.Id}?thumb=true" : null
                    })
                });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("add-collection-product-image")]
        public async Task<IActionResult> AddCollectionProductImage([FromBody] AddCollectionProductImageRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.CollectionId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "CollectionId is required." });

            try
            {
                var collection = await _projectCollectionRepository.GetByIdAsync(request.CollectionId);
                if (collection == null || collection.Status != 1)
                    return Json(new ApiResponse { success = false, message = "Collection not found." });

                var project = await _projectRepository.GetByIdAsync(collection.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Collection not found." });

                var image = await _projectCollectionProductImageRepository.CreateAsync(new ProjectCollectionProductImage
                {
                    ProjectId = request.ProjectId,
                    CollectionId = request.CollectionId,
                    ProjectBlueprintId = request.ProjectBlueprintId,
                    ProductImageId = Guid.Empty,
                    ImageModel = "",
                    Prompt = "",
                    Width = 0,
                    Height = 0,
                    Accepted = false,
                    ResponseId = "",
                    VariantColor = request.Title,
                    Active = true,
                    SelectedMockups = "",
                    Generated = false
                });

                return Json(new ApiResponse { success = true, data = new { id = image.Id, variantColor = image.VariantColor } });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("update-collection-product-image-config")]
        public async Task<IActionResult> UpdateCollectionProductImageConfig([FromBody] UpdateCollectionProductImageConfigRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.Id == Guid.Empty || request.CollectionId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Id and CollectionId are required." });

            try
            {
                var image = await _projectCollectionProductImageRepository.GetByIdAsync(request.Id);
                if (image == null || image.CollectionId != request.CollectionId)
                    return Json(new ApiResponse { success = false, message = "Product image not found." });

                var project = await _projectRepository.GetByIdAsync(image.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                await _projectCollectionProductImageRepository.UpdateConfigAsync(request.Id, request.VariantColor, request.ImageModel, request.Prompt, request.SelectedMockups, request.IncludeArtworkRef);

                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("delete-collection-product-image")]
        public async Task<IActionResult> DeleteCollectionProductImage([FromBody] DeleteCollectionProductImageByIdRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.Id == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Id is required." });

            try
            {
                var image = await _projectCollectionProductImageRepository.GetByIdAsync(request.Id);
                if (image == null)
                    return Json(new ApiResponse { success = false, message = "Product image not found." });

                var project = await _projectRepository.GetByIdAsync(image.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                await _projectCollectionProductImageRepository.DeleteAsync(request.Id);

                return Json(new ApiResponse { success = true });
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
                        await _productRepository.UpdateActiveAsync(existing.Id, existing.Active);
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

        [HttpGet("get-collection-product-details")]
        public async Task<IActionResult> GetCollectionProductDetails([FromQuery] Guid collectionId, [FromQuery] Guid projectBlueprintId)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (collectionId == Guid.Empty || projectBlueprintId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Collection ID and Project Blueprint ID are required." });

            try
            {
                var product = await _productRepository.GetByCollectionAndBlueprintIdAsync(collectionId, projectBlueprintId);
                if (product == null)
                    return Json(new ApiResponse { success = false, message = "Product not found." });

                var bp = await _projectBlueprintRepository.GetByIdAsync(projectBlueprintId);
                var variants = new List<object>();
                if (bp != null && bp.BlueprintId > 0)
                {
                    var allVariants = await _variantRepository.GetByBlueprintIdAsync(bp.BlueprintId);
                    var selectedVariantIds = new HashSet<int>();
                    try
                    {
                        var cfg = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(bp.BlueprintJson);
                        if (cfg != null && cfg.TryGetValue("variantIds", out var variantEl) && variantEl.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var v in variantEl.EnumerateArray())
                                selectedVariantIds.Add(v.GetInt32());
                        }
                    }
                    catch { }

                    var priceMap = new Dictionary<int, decimal>();
                    try
                    {
                        var pricing = JsonSerializer.Deserialize<List<JsonElement>>(product.PricingJson ?? "[]");
                        if (pricing != null)
                        {
                            foreach (var p in pricing)
                            {
                                if (p.TryGetProperty("variantId", out var vidEl) && p.TryGetProperty("price", out var priceEl))
                                    priceMap[vidEl.GetInt32()] = priceEl.GetDecimal();
                            }
                        }
                    }
                    catch { }

                    variants = allVariants
                        .Where(v => selectedVariantIds.Contains(v.VariantId))
                        .Select(v => new
                        {
                            id = v.VariantId,
                            color = v.Color,
                            size = v.Size,
                            price = priceMap.TryGetValue(v.VariantId, out var price) ? price : 0,
                        })
                        .Cast<object>()
                        .ToList();
                }

                return Json(new ApiResponse
                {
                    success = true,
                    data = new
                    {
                        id = product.Id,
                        name = product.Name,
                        description = product.Description,
                        safetyInfo = product.SafetyInfo,
                        pricingJson = product.PricingJson,
                        variants
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("update-collection-product-details")]
        public async Task<IActionResult> UpdateCollectionProductDetails([FromBody] UpdateCollectionProductDetailsRequest request)
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
                product.Description = request.Description ?? "";
                product.SafetyInfo = request.SafetyInfo ?? "";
                product.PricingJson = request.PricingJson ?? "[]";
                await _productRepository.UpdateAsync(product);

                // If requested, update the Printify product with only the changed fields
                if (request.UpdatePrintify && request.ChangedFields.Count > 0)
                {
                    var pp = (await _printifyProductRepository.GetByCollectionIdAsync(request.CollectionId))
                        .FirstOrDefault(p => p.ProductId == product.Id);
                    if (pp != null && !string.IsNullOrWhiteSpace(pp.PrintifyProductId))
                    {
                        var includeTitle = request.ChangedFields.Contains("name", StringComparer.OrdinalIgnoreCase);
                        var includeDescription = request.ChangedFields.Contains("description", StringComparer.OrdinalIgnoreCase);
                        var includeSafety = request.ChangedFields.Contains("safetyInfo", StringComparer.OrdinalIgnoreCase);
                        var includePricing = request.ChangedFields.Contains("pricing", StringComparer.OrdinalIgnoreCase);

                        // Build a partial JSON with only the changed fields to avoid sending
                        // blueprint_id=0 / print_provider_id=0 which Printify would reject
                        var partial = new Dictionary<string, object>();

                        if (includeTitle)
                            partial["title"] = product.Name;
                        if (includeDescription)
                        {
                            var desc = product.Description ?? "";
                            var disclaimer = "Disclaimer: The artworks printed on this product were generated using AI. The products and any humans and environments within the mockup images were also generated using AI. The real-world product may appear slightly different from these mockup images as a result.";
                            if (!desc.Contains("Disclaimer: The artworks printed on this product were generated using AI"))
                            {
                                if (!string.IsNullOrWhiteSpace(desc))
                                    desc += "\n\n";
                                desc += disclaimer;
                            }
                            partial["description"] = desc;
                        }
                        if (includeSafety)
                            partial["safety_information"] = product.SafetyInfo;

                        if (includePricing)
                        {
                            var bp = await _projectBlueprintRepository.GetByIdAsync(request.ProjectBlueprintId);
                            if (bp != null)
                            {
                                var selectedVariantIds = new List<int>();
                                try
                                {
                                    var cfg = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(bp.BlueprintJson);
                                    if (cfg != null && cfg.TryGetValue("variantIds", out var variantEl) && variantEl.ValueKind == JsonValueKind.Array)
                                        selectedVariantIds = variantEl.EnumerateArray().Select(v => v.GetInt32()).ToList();
                                }
                                catch { }

                                var priceMap = new Dictionary<int, int>();
                                try
                                {
                                    var pricing = JsonSerializer.Deserialize<List<JsonElement>>(product.PricingJson);
                                    if (pricing != null)
                                    {
                                        foreach (var p in pricing)
                                        {
                                            if (p.TryGetProperty("variantId", out var vidEl) && p.TryGetProperty("price", out var priceEl))
                                                priceMap[vidEl.GetInt32()] = (int)Math.Round(priceEl.GetDecimal() * 100);
                                        }
                                    }
                                }
                                catch { }

                                partial["variants"] = selectedVariantIds.Select(vid => new
                                {
                                    id = vid,
                                    price = priceMap.TryGetValue(vid, out var price) ? price : 0,
                                    is_enabled = true,
                                }).ToList();
                            }
                        }

                        if (partial.Count > 0)
                        {
                            var jsonBody = JsonSerializer.Serialize(partial);
                            var printifyResult = await _printifyService.UpdateProductAsync(userId, pp.PrintifyShopId, pp.PrintifyProductId, jsonBody);
                            if (printifyResult == null)
                                return Json(new ApiResponse { success = false, message = "Product details saved, but failed to update the product on Printify. Check server logs for details." });
                        }
                    }
                }

                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("generate-collection-product-info")]
        public async Task<IActionResult> GenerateCollectionProductInfo([FromBody] GenerateCollectionProductInfoRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.CollectionId == Guid.Empty || request.ProjectBlueprintId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Collection ID and Project Blueprint ID are required." });

            try
            {
                var bp = await _projectBlueprintRepository.GetByIdAsync(request.ProjectBlueprintId);
                if (bp == null)
                    return Json(new ApiResponse { success = false, message = "Blueprint not found." });

                var collection = await _projectCollectionRepository.GetByIdAsync(request.CollectionId);
                if (collection == null)
                    return Json(new ApiResponse { success = false, message = "Collection not found." });

                var project = await _projectRepository.GetByIdAsync(collection.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                var printifyBlueprint = bp.BlueprintId > 0
                    ? await _printifyBlueprintRepository.GetByBlueprintIdAsync(bp.BlueprintId)
                    : null;
                var bpTitle = printifyBlueprint?.Title ?? "";
                var bpDescription = printifyBlueprint?.Description ?? "";

                // Collect selected variant colors
                var selectedColors = new List<string>();
                try
                {
                    var cfg = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(bp.BlueprintJson);
                    if (cfg != null && cfg.TryGetValue("variantIds", out var variantEl) && variantEl.ValueKind == JsonValueKind.Array)
                    {
                        var variantIds = variantEl.EnumerateArray().Select(v => v.GetInt32()).ToList();
                        var allVariants = await _variantRepository.GetByBlueprintIdAsync(bp.BlueprintId);
                        var variantMap = allVariants.ToDictionary(v => v.VariantId);
                        var colorSet = new HashSet<string>();
                        foreach (var vid in variantIds)
                        {
                            if (variantMap.TryGetValue(vid, out var v) && !string.IsNullOrWhiteSpace(v.Color))
                                colorSet.Add(v.Color);
                        }
                        selectedColors = colorSet.OrderBy(c => c).ToList();
                    }
                }
                catch { }
                var colorsDelimited = selectedColors.Count > 0 ? string.Join(", ", selectedColors) : "N/A";

                // Collect artwork prompts
                var artworkPrompts = new List<string>();
                try
                {
                    var placements = JsonSerializer.Deserialize<List<JsonElement>>(bp.PlacementJson);
                    if (placements != null)
                    {
                        var itemIds = new HashSet<Guid>();
                        foreach (var p in placements)
                        {
                            if (p.TryGetProperty("source", out var srcEl) && srcEl.GetString() == "item" &&
                                p.TryGetProperty("itemId", out var itemEl) && itemEl.ValueKind != JsonValueKind.Null)
                            {
                                if (Guid.TryParse(itemEl.GetString(), out var itemId))
                                    itemIds.Add(itemId);
                            }
                        }

                        var seenArtworkIds = new HashSet<Guid>();
                        foreach (var itemId in itemIds)
                        {
                            var itemArtworks = await _projectItemArtworkRepository.GetByItemIdAsync(itemId);
                            foreach (var a in itemArtworks)
                            {
                                if (string.IsNullOrWhiteSpace(a.Prompt) || !seenArtworkIds.Add(a.Id))
                                    continue;
                                artworkPrompts.Add(a.Prompt);
                            }
                        }
                    }
                }
                catch { }
                var artworkPromptsText = artworkPrompts.Count > 0
                    ? string.Join("\n", artworkPrompts.Select((p, i) => $"{i + 1}. {p}"))
                    : "N/A";

                // Collect project Q&A and artwork Q&A from the collection
                var answers = await _projectCollectionAnswerRepository.GetByCollectionIdAsync(request.CollectionId);
                var projectQuestions = await _projectQuestionRepository.GetByProjectIdAsync(collection.ProjectId);
                var projectQuestionMap = projectQuestions.ToDictionary(q => q.Id);
                var itemQuestions = await _projectItemQuestionRepository.GetByProjectIdAsync(collection.ProjectId);
                var itemQuestionMap = itemQuestions.ToDictionary(q => q.Id);

                var qaText = new StringBuilder();
                foreach (var ans in answers)
                {
                    if (ans.QuestionId.HasValue && projectQuestionMap.TryGetValue(ans.QuestionId.Value, out var pq))
                    {
                        qaText.AppendLine($"Q: {pq.Question}");
                        qaText.AppendLine($"A: {ans.Answer}");
                        qaText.AppendLine();
                    }
                    else if (ans.QuestionId.HasValue && itemQuestionMap.TryGetValue(ans.QuestionId.Value, out var iq))
                    {
                        qaText.AppendLine($"Q: {iq.Question}");
                        qaText.AppendLine($"A: {ans.Answer}");
                        qaText.AppendLine();
                    }
                }
                var qaTextStr = qaText.Length > 0 ? qaText.ToString() : "N/A";

                var systemPrompt = "You are a product copywriter for a print-on-demand store. " +
                    "Given context about a product, generate a compelling product title and description. " +
                    "The title should be concise (max 80 characters) and suitable for an e-commerce listing. " +
                    "The description should be 2-4 short paragraphs, written in plain text (no HTML), highlighting the product's appeal. " +
                    "Return ONLY a JSON object with no markdown formatting, in the following structure:\n" +
                    "{\"title\":\"\",\"description\":\"\"}";

                var userPrompt = $"We are generating a title & description for a product.\n\n" +
                    $"Printify Blueprint Name: {bpTitle}\n" +
                    $"Printify Blueprint Description: {bpDescription}\n\n" +
                    $"Project Name: {project.Title}\n" +
                    $"Project Description: {project.Description ?? "N/A"}\n\n" +
                    $"Selected Variant Colors: {colorsDelimited}\n\n" +
                    $"Artwork Prompts:\n{artworkPromptsText}\n\n" +
                    $"Collection Q&A:\n{qaTextStr}\n\n" +
                    $"Generate a product title and description that would appeal to buyers of this print-on-demand product.";

                string llmOutput;
                try
                {
                    llmOutput = await OpenAI.Prompt(systemPrompt, "", userPrompt, seed: (long)Random.Shared.Next(1, int.MaxValue));
                }
                catch (Exception ex)
                {
                    return Json(new ApiResponse { success = false, message = $"LLM generation failed: {ex.Message}" });
                }

                var rawJson = ExtractFirstJsonObject(llmOutput) ?? llmOutput.Trim();
                string genTitle = "";
                string genDescription = "";
                try
                {
                    using var doc = JsonDocument.Parse(rawJson);
                    if (doc.RootElement.TryGetProperty("title", out var tEl))
                        genTitle = tEl.GetString() ?? "";
                    if (doc.RootElement.TryGetProperty("description", out var dEl))
                        genDescription = dEl.GetString() ?? "";
                }
                catch
                {
                    return Json(new ApiResponse { success = false, message = "Failed to parse LLM response." });
                }

                return Json(new ApiResponse
                {
                    success = true,
                    data = new { title = genTitle, description = genDescription }
                });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("get-multi-product-json")]
        public async Task<IActionResult> GetMultiProductJson([FromQuery] Guid collectionId)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (collectionId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Collection ID is required." });

            try
            {
                var collection = await _projectCollectionRepository.GetByIdAsync(collectionId);
                if (collection == null)
                    return Json(new ApiResponse { success = false, message = "Collection not found." });

                var project = await _projectRepository.GetByIdAsync(collection.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                return Json(new ApiResponse
                {
                    success = true,
                    data = new { multiProductJson = collection.MultiProductJson ?? "" }
                });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("save-multi-product-json")]
        public async Task<IActionResult> SaveMultiProductJson([FromBody] SaveMultiProductJsonRequest request)
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
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                await _projectCollectionRepository.UpdateMultiProductJsonAsync(request.CollectionId, request.MultiProductJson);

                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("generate-multi-product-info")]
        public async Task<IActionResult> GenerateMultiProductInfo([FromBody] GenerateMultiProductInfoRequest request)
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
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                var products = await _productRepository.GetByCollectionIdAsync(request.CollectionId);
                var activeProducts = products.Where(p => p.Active).ToList();
                if (activeProducts.Count == 0)
                    return Json(new ApiResponse { success = false, message = "No active products found in collection." });

                var productsText = new StringBuilder();
                for (var i = 0; i < activeProducts.Count; i++)
                {
                    productsText.AppendLine($"Product {i + 1}:");
                    productsText.AppendLine($"  Name: {activeProducts[i].Name}");
                    productsText.AppendLine($"  Description: {activeProducts[i].Description}");
                    productsText.AppendLine();
                }

                // --- Prompt 1: Title & Description (skip if tagsOnly) ---
                string genTitle = request.TagsOnly ? (request.Title ?? "") : "";
                string genDescription = "";
                if (!request.TagsOnly)
                {
                    var titleDescSystemPrompt = "You are a product copywriter for a print-on-demand e-commerce store. " +
                        "Given information about multiple products that will be combined into a single multi-product listing, " +
                        "generate a compelling listing title and description. " +
                        "The title should be concise (max 80 characters) and suitable for an e-commerce multi-product listing. " +
                        "The description should be 2-4 short paragraphs, written in plain text (no HTML), highlighting the combined appeal of all products. " +
                        "At the end of the description, add a section titled \"The Collection\" (on its own line), followed by one line per product " +
                        "in the format: \"Product Name — a single sentence describing the product\". Each product line should be concise and highlight the product's key feature. " +
                        "Return ONLY a JSON object with no markdown formatting, in the following structure:\n" +
                        "{\"title\":\"\",\"description\":\"\"}";

                    var titleDescUserPrompt = $"We are generating a title and description for a multi-product listing that combines all the following products into one listing.\n\n" +
                        $"Project Name: {project.Title}\n" +
                        $"Collection Title: {collection.Title}\n\n" +
                        $"Products to combine:\n{productsText}\n\n" +
                        $"Generate a product title and description that would appeal to buyers of this multi-product print-on-demand listing.";

                    string titleDescLlmOutput;
                    try
                    {
                        titleDescLlmOutput = await OpenAI.Prompt(titleDescSystemPrompt, "", titleDescUserPrompt, seed: (long)Random.Shared.Next(1, int.MaxValue));
                    }
                    catch (Exception ex)
                    {
                        return Json(new ApiResponse { success = false, message = $"LLM generation failed: {ex.Message}" });
                    }

                    var titleDescRawJson = ExtractFirstJsonObject(titleDescLlmOutput) ?? titleDescLlmOutput.Trim();
                    try
                    {
                        using var doc = JsonDocument.Parse(titleDescRawJson);
                        if (doc.RootElement.TryGetProperty("title", out var tEl))
                            genTitle = tEl.GetString() ?? "";
                        if (doc.RootElement.TryGetProperty("description", out var dEl))
                            genDescription = dEl.GetString() ?? "";
                    }
                    catch
                    {
                        return Json(new ApiResponse { success = false, message = "Failed to parse LLM response for title & description." });
                    }
                }

                // --- Prompt 2: Tags ---
                var tagsSystemPrompt = "You are an expert product tagging specialist. Your role is to generate exactly 50 unique, high-performing tags for product listings based on proven tagging best practices.\n\n" +
                    "CORE TAGGING PRINCIPLES\n\n" +
                    "1. Use all 50 tags. Each tag is an opportunity to match with a shopper's search. No fewer than 50.\n" +
                    "2. Use multi-word phrases. Tags should be 2-3 word phrases, up to 20 characters. Prioritize phrases over single words.\n" +
                    "3. Ensure complete uniqueness. No tag should repeat or overlap with another. Each of the 50 tags must be distinct and non-redundant.\n" +
                    "4. Target long-tail keywords. Avoid generic, high-competition searches. Instead, prioritize specific, descriptive phrases that narrow down to what's truly unique about the product.\n" +
                    "5. Consider regional variants. If the product appeals to international shoppers, include regional spellings or phrases they might search for.\n" +
                    "6. Don't repeat categories. If the product's category already includes a phrase, don't tag it again.\n\n" +
                    "TAG CATEGORIES TO EXPLORE\n\n" +
                    "Generate tags across these categories to ensure diversity:\n\n" +
                    "Descriptive: Multi-word descriptions of what the product is. Aim for 5-7 tags.\n\n" +
                    "Materials and Techniques: How it's made, special construction, materials used, custom or personalized elements. Aim for 5-7 tags.\n\n" +
                    "Who It's For: Target customer, gift recipients, lifestyle, demographics. Aim for 5-7 tags.\n\n" +
                    "Shopping Occasions: When or why someone might buy it. Think holidays, life events, seasonal uses. Aim for 5-7 tags.\n\n" +
                    "Solution-Oriented: What problem does it solve? What need does it fulfill? Aim for 5-7 tags.\n\n" +
                    "Aesthetic and Style: Design style, color palette, mood, time period, vibe. Aim for 5-7 tags.\n\n" +
                    "Size and Format: Scale, dimensions, portability if relevant. Aim for 3-5 tags.\n\n" +
                    "Use Cases: Specific activities or situations where the product excels. Aim for 3-5 tags.\n\n" +
                    "TAGGING DON'TS\n\n" +
                    "Do not include misspellings intentionally.\n" +
                    "Do not add tags in multiple languages.\n" +
                    "Do not worry about plural versus singular forms.\n" +
                    "Do not repeat the exact same word or phrase twice.\n" +
                    "Do not use single-word tags when a phrase would be stronger.\n\n" +
                    "OUTPUT FORMAT\n\n" +
                    "Return exactly 50 tags as a comma-delimited list. Each tag should be clearly distinct, strategically diverse across categories, and optimized for search relevance and specificity.\n\n" +
                    "Return ONLY a JSON object with no markdown formatting, in the following structure:\n" +
                    "{\"tags\":\"\"}";

                var tagsUserPrompt = $"We are generating tags for a multi-product listing that combines all the following products into one listing.\n\n" +
                    $"Listing Title: {genTitle}\n" +
                    $"Project Name: {project.Title}\n" +
                    $"Collection Title: {collection.Title}\n\n" +
                    $"Products to combine:\n{productsText}\n\n" +
                    "Generate exactly 50 comma-delimited tags for this multi-product listing.";

                string tagsLlmOutput;
                try
                {
                    tagsLlmOutput = await OpenAI.Prompt(tagsSystemPrompt, "", tagsUserPrompt, seed: (long)Random.Shared.Next(1, int.MaxValue));
                }
                catch (Exception ex)
                {
                    return Json(new ApiResponse { success = false, message = $"LLM tag generation failed: {ex.Message}" });
                }

                var tagsRawJson = ExtractFirstJsonObject(tagsLlmOutput) ?? tagsLlmOutput.Trim();
                string genTags = "";
                try
                {
                    using var doc = JsonDocument.Parse(tagsRawJson);
                    if (doc.RootElement.TryGetProperty("tags", out var tagEl))
                        genTags = tagEl.GetString() ?? "";
                }
                catch
                {
                    return Json(new ApiResponse { success = false, message = "Failed to parse LLM response for tags." });
                }

                var disclaimer = "Disclaimer: The artworks printed on this product were generated using AI. The products and any humans and environments within the mockup images were also generated using AI. The real-world product may appear slightly different from these mockup images as a result.";
                if (!string.IsNullOrWhiteSpace(genDescription) && !genDescription.Contains("Disclaimer: The artworks printed on this product were generated using AI"))
                {
                    genDescription += "\n\n" + disclaimer;
                }

                return Json(new ApiResponse
                {
                    success = true,
                    data = request.TagsOnly
                        ? new { tags = genTags }
                        : new { title = genTitle, description = genDescription, tags = genTags }
                });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("get-collection-artwork-references")]
        public async Task<IActionResult> GetCollectionArtworkReferences([FromQuery] Guid collectionId, [FromQuery] Guid itemId)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (collectionId == Guid.Empty || itemId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Collection ID and Item ID are required." });

            try
            {
                var references = await _projectCollectionArtworkReferenceRepository.GetByCollectionAndItemIdAsync(collectionId, itemId);
                var result = new List<object>();
                foreach (var r in references)
                {
                    var customImg = await _customImageRepository.GetByIdAsync(r.CustomImageId);
                    result.Add(new
                    {
                        r.Id,
                        r.CollectionId,
                        r.ProjectId,
                        r.ItemId,
                        r.CustomImageId,
                        customImg?.FileName,
                        customImg?.Extension,
                        r.Created,
                    });
                }
                return Json(new ApiResponse { success = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("add-collection-artwork-reference")]
        public async Task<IActionResult> AddCollectionArtworkReference([FromBody] AddCollectionArtworkReferenceRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.CollectionId == Guid.Empty || request.ItemId == Guid.Empty || request.CustomImageId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Collection ID, Item ID, and Custom Image ID are required." });

            try
            {
                var collection = await _projectCollectionRepository.GetByIdAsync(request.CollectionId);
                if (collection == null)
                    return Json(new ApiResponse { success = false, message = "Collection not found." });

                var customImg = await _customImageRepository.GetByIdAsync(request.CustomImageId);
                if (customImg == null)
                    return Json(new ApiResponse { success = false, message = "Custom image not found." });

                // Check for duplicate
                var existing = await _projectCollectionArtworkReferenceRepository.GetByCollectionAndItemIdAsync(request.CollectionId, request.ItemId);
                if (existing.Any(r => r.CustomImageId == request.CustomImageId))
                    return Json(new ApiResponse { success = false, message = "This image is already added as a reference." });

                var reference = await _projectCollectionArtworkReferenceRepository.CreateAsync(new ProjectCollectionArtworkReference
                {
                    CollectionId = request.CollectionId,
                    ProjectId = collection.ProjectId,
                    ItemId = request.ItemId,
                    CustomImageId = request.CustomImageId,
                    Created = DateTime.UtcNow,
                });

                return Json(new ApiResponse
                {
                    success = true,
                    data = new
                    {
                        reference.Id,
                        reference.CollectionId,
                        reference.ProjectId,
                        reference.ItemId,
                        reference.CustomImageId,
                        customImg.FileName,
                        customImg.Extension,
                        reference.Created,
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("delete-collection-artwork-reference")]
        public async Task<IActionResult> DeleteCollectionArtworkReference([FromBody] DeleteCollectionArtworkReferenceRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.Id == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Reference ID is required." });

            try
            {
                var reference = await _projectCollectionArtworkReferenceRepository.GetByIdAsync(request.Id);
                if (reference == null)
                    return Json(new ApiResponse { success = false, message = "Reference not found." });

                await _projectCollectionArtworkReferenceRepository.DeleteAsync(request.Id);
                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

    }
}
