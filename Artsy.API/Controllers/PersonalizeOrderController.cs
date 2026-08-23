using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Artsy.API.Models;
using Artsy.API.Models.Collections;
using Artsy.API.Models.Orders;
using Artsy.API.Models.Projects;
using Artsy.API.Services;
using Artsy.Data.Entities;
using Artsy.Data.Entities.Orders;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces;
using Artsy.Data.Interfaces.Orders;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.API.Controllers
{
    [Authorize]
    [Route("/api/personalize-order")]
    public class PersonalizeOrderController : ApiController
    {
        readonly IOrderRepository _orderRepository;
        readonly IProjectCollectionProductRepository _projectCollectionProductRepository;
        readonly IProjectCollectionProductPlacementRepository _placementRepository;
        readonly IProjectCollectionArtworkRepository _collectionArtworkRepository;
        readonly IProjectBlueprintsRepository _projectBlueprintsRepository;
        readonly IProjectItemRepository _projectItemRepository;
        readonly IProjectItemArtworkRepository _projectItemArtworkRepository;
        readonly IProjectItemReferenceRepository _projectItemReferenceRepository;
        readonly IProjectQuestionRepository _projectQuestionRepository;
        readonly IProjectCollectionAnswerRepository _projectCollectionAnswerRepository;
        readonly IOrderItemAnswerRepository _orderItemAnswerRepository;
        readonly IPrintifyBlueprintVariantPlaceholderRepository _placeholderRepository;
        readonly IOrderItemArtworkRepository _orderItemArtworkRepository;
        readonly IOrderItemArtworkPlacementRepository _orderItemArtworkPlacementRepository;
        readonly IImageGenerationModelRepository _imageGenerationModelRepository;
        readonly IEnumerable<IImageGeneration> _imageGenerations;
        readonly IImageService _imageService;
        readonly ICustomImageRepository _customImageRepository;
        readonly IOpacityService _opacityService;
        readonly IArtworkGenerationPlanService _artworkGenerationPlanService;
        readonly TokenCostOptions _tokenCostOptions;

        public PersonalizeOrderController(
            IOrderRepository orderRepository,
            IProjectCollectionProductRepository projectCollectionProductRepository,
            IProjectCollectionProductPlacementRepository placementRepository,
            IProjectCollectionArtworkRepository collectionArtworkRepository,
            IProjectBlueprintsRepository projectBlueprintsRepository,
            IProjectItemRepository projectItemRepository,
            IProjectItemArtworkRepository projectItemArtworkRepository,
            IProjectItemReferenceRepository projectItemReferenceRepository,
            IProjectQuestionRepository projectQuestionRepository,
            IProjectCollectionAnswerRepository projectCollectionAnswerRepository,
            IOrderItemAnswerRepository orderItemAnswerRepository,
            IPrintifyBlueprintVariantPlaceholderRepository placeholderRepository,
            IOrderItemArtworkRepository orderItemArtworkRepository,
            IOrderItemArtworkPlacementRepository orderItemArtworkPlacementRepository,
            IImageGenerationModelRepository imageGenerationModelRepository,
            IEnumerable<IImageGeneration> imageGenerations,
            IImageService imageService,
            ICustomImageRepository customImageRepository,
            IOpacityService opacityService,
            IArtworkGenerationPlanService artworkGenerationPlanService,
            IOptions<TokenCostOptions> tokenCostOptions)
        {
            _orderRepository = orderRepository;
            _projectCollectionProductRepository = projectCollectionProductRepository;
            _placementRepository = placementRepository;
            _collectionArtworkRepository = collectionArtworkRepository;
            _projectBlueprintsRepository = projectBlueprintsRepository;
            _projectItemRepository = projectItemRepository;
            _projectItemArtworkRepository = projectItemArtworkRepository;
            _projectItemReferenceRepository = projectItemReferenceRepository;
            _projectQuestionRepository = projectQuestionRepository;
            _projectCollectionAnswerRepository = projectCollectionAnswerRepository;
            _orderItemAnswerRepository = orderItemAnswerRepository;
            _placeholderRepository = placeholderRepository;
            _orderItemArtworkRepository = orderItemArtworkRepository;
            _orderItemArtworkPlacementRepository = orderItemArtworkPlacementRepository;
            _imageGenerationModelRepository = imageGenerationModelRepository;
            _imageGenerations = imageGenerations;
            _imageService = imageService;
            _customImageRepository = customImageRepository;
            _opacityService = opacityService;
            _artworkGenerationPlanService = artworkGenerationPlanService;
            _tokenCostOptions = tokenCostOptions.Value;
        }

        [HttpGet("{orderId}/items/{orderItemId}/placements")]
        public async Task<IActionResult> GetOrderItemPlacements(Guid orderId, Guid orderItemId)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var order = await _orderRepository.GetByIdWithDetailsAsync(orderId);
            if (order == null || order.Order.AppUserId != userId)
                return NotFound();

            var item = order.Items.FirstOrDefault(i => i.Id == orderItemId);
            if (item == null)
                return NotFound();

            var cp = await _projectCollectionProductRepository.GetByIdAsync(item.CollectionProductId);
            if (cp == null)
                return Json(new { success = true, data = new { collectionProduct = (object?)null, placements = new List<object>() } });

            var allCollectionArtwork = (await _collectionArtworkRepository.GetByCollectionIdAsync(cp.CollectionId)).ToList();
            var allPlaceholders = (await _placeholderRepository.GetByVariantIdAsync(item.VariantId)).ToList();
            var projectItems = (await _projectItemRepository.GetByProjectIdAsync(cp.ProjectId)).ToDictionary(pi => pi.Id);

            var placements = (await _placementRepository.GetByProductIdAndVariantIdAsync(cp.Id, item.VariantId))
                .Where(p => p.ArtworkId != Guid.Empty)
                .ToList();
            List<object> result;

            if (placements.Count > 0)
            {
                var artworkIds = placements.Select(p => p.ArtworkId).Distinct().ToList();
                var artworks = allCollectionArtwork
                    .Where(a => artworkIds.Contains(a.Id))
                    .ToDictionary(a => a.Id);

                result = placements.Select(p =>
                {
                    artworks.TryGetValue(p.ArtworkId, out var artwork);
                    var placeholder = allPlaceholders.FirstOrDefault(ph => ph.Position == p.Position);
                    var projectItem = artwork != null && projectItems.TryGetValue(artwork.ItemId, out var pi) ? pi : null;
                    return (object)new
                    {
                        p.Id,
                        p.Position,
                        p.ArtworkId,
                        placementIndex = p.PlacementIndex,
                        artworkItemId = artwork?.ItemId,
                        artworkItemTitle = projectItem?.Title,
                        artworkItemIndex = projectItem?.Index ?? 0,
                        artworkImageModel = artwork?.ImageModel,
                        artworkPrompt = artwork?.Prompt,
                        artworkAccepted = artwork?.Accepted,
                        artworkFullSize = artwork?.FullSize,
                        artworkTotalPlacements = artwork?.TotalPlacements ?? 1,
                        placeholder = placeholder == null ? null : new { placeholder.Position, placeholder.Width, placeholder.Height, placeholder.DecorationMethod },
                    };
                }).ToList();
            }
            else
            {
                var bp = await _projectBlueprintsRepository.GetByIdAsync(cp.ProjectBlueprintId);
                if (bp != null && !string.IsNullOrWhiteSpace(bp.PlacementJson))
                {
                    var placementDtos = (JsonSerializer.Deserialize<List<PlacementDto>>(bp.PlacementJson) ?? new List<PlacementDto>())
                        .Where(p => !string.Equals(p.Source, "custom", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    result = placementDtos.Select(p =>
                    {
                        var itemId = p.GetItemId();
                        var artwork = itemId != Guid.Empty
                            ? allCollectionArtwork.FirstOrDefault(a => a.ItemId == itemId && a.Active)
                            : null;
                        var (w, h) = p.GetDimensions();
                        var placeholder = allPlaceholders.FirstOrDefault(ph => ph.Position == p.Position);
                        var width = placeholder?.Width ?? w;
                        var height = placeholder?.Height ?? h;
                        var projectItem = artwork != null && projectItems.TryGetValue(artwork.ItemId, out var pi) ? pi : null;
                        return (object)new
                        {
                            Id = Guid.NewGuid(),
                            Position = p.Position,
                            Source = p.Source,
                            ArtworkId = artwork?.Id ?? Guid.Empty,
                            artworkItemId = artwork?.ItemId,
                            artworkItemTitle = projectItem?.Title,
                            artworkItemIndex = projectItem?.Index ?? 0,
                            artworkImageModel = artwork?.ImageModel,
                            artworkPrompt = artwork?.Prompt,
                            artworkAccepted = artwork?.Accepted,
                            artworkFullSize = artwork?.FullSize,
                            placeholder = new { p.Position, Width = width, Height = height, p.DecorationMethod },
                        };
                    }).ToList();
                }
                else
                {
                    result = allPlaceholders.Select(ph => (object)new
                    {
                        Id = Guid.NewGuid(),
                        Position = ph.Position,
                        ArtworkId = Guid.Empty,
                        artworkItemId = (Guid?)null,
                        artworkItemTitle = (string?)null,
                        artworkItemIndex = 0,
                        artworkImageModel = (string?)null,
                        artworkPrompt = (string?)null,
                        artworkAccepted = (bool?)null,
                        artworkFullSize = (bool?)null,
                        placeholder = new { ph.Position, ph.Width, ph.Height, ph.DecorationMethod },
                    }).ToList();
                }
            }

            return Json(new { success = true, data = new { collectionProduct = cp, orderItem = item, placements = result } });
        }

        [HttpGet("{orderId}/items/{orderItemId}/project-questions")]
        public async Task<IActionResult> GetProjectQuestions(Guid orderId, Guid orderItemId)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var order = await _orderRepository.GetByIdWithDetailsAsync(orderId);
            if (order == null || order.Order.AppUserId != userId)
                return NotFound();

            var item = order.Items.FirstOrDefault(i => i.Id == orderItemId);
            if (item == null)
                return NotFound();

            var cp = await _projectCollectionProductRepository.GetByIdAsync(item.CollectionProductId);
            if (cp == null)
                return Json(new { success = false, message = "Collection product not found." });

            var questions = (await _projectQuestionRepository.GetByProjectIdAsync(cp.ProjectId)).OrderBy(q => q.Index).ToList();
            var savedAnswers = (await _orderItemAnswerRepository.GetByOrderItemIdAsync(orderItemId)).ToList();

            if (!savedAnswers.Any())
            {
                var collectionAnswers = (await _projectCollectionAnswerRepository.GetByCollectionIdAsync(cp.CollectionId)).ToList();
                var fallbackAnswers = collectionAnswers
                    .Select(a => new { a.QuestionId, a.ItemId, a.Answer })
                    .ToList();
                return Json(new
                {
                    success = true,
                    data = new
                    {
                        questions = questions.Select(q => new { q.Id, q.Question }),
                        answers = fallbackAnswers
                    }
                });
            }

            return Json(new
            {
                success = true,
                data = new
                {
                    questions = questions.Select(q => new { q.Id, q.Question }),
                    answers = savedAnswers.Select(a => new { a.QuestionId, a.ItemId, a.Answer })
                }
            });
        }

        [HttpPost("{orderId}/items/{orderItemId}/project-questions")]
        public async Task<IActionResult> SaveProjectQuestions(Guid orderId, Guid orderItemId, [FromBody] SaveOrderItemAnswersRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var order = await _orderRepository.GetByIdWithDetailsAsync(orderId);
            if (order == null || order.Order.AppUserId != userId)
                return NotFound();

            var item = order.Items.FirstOrDefault(i => i.Id == orderItemId);
            if (item == null)
                return NotFound();

            var cp = await _projectCollectionProductRepository.GetByIdAsync(item.CollectionProductId);
            if (cp == null)
                return Json(new { success = false, message = "Collection product not found." });

            foreach (var a in request.Answers)
            {
                await _orderItemAnswerRepository.UpsertAsync(new OrderItemAnswer
                {
                    OrderItemId = orderItemId,
                    ProjectId = cp.ProjectId,
                    QuestionId = a.QuestionId,
                    ItemId = a.ItemId,
                    Answer = a.Answer ?? ""
                });
            }

            return Json(new { success = true });
        }

        [HttpGet("{orderId}/items/{orderItemId}/estimate-token")]
        public async Task<IActionResult> EstimateOrderItemToken(Guid orderId, Guid orderItemId, [FromQuery] Guid artworkItemId, [FromQuery] int modelId)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            if (artworkItemId == Guid.Empty)
                return Json(new { success = false, message = "Artwork item ID is required." });

            var order = await _orderRepository.GetByIdWithDetailsAsync(orderId);
            if (order == null || order.Order.AppUserId != userId)
                return NotFound();

            var item = order.Items.FirstOrDefault(i => i.Id == orderItemId);
            if (item == null)
                return NotFound();

            var cp = await _projectCollectionProductRepository.GetByIdAsync(item.CollectionProductId);
            if (cp == null)
                return Json(new { success = false, message = "Collection product not found." });

            // Build the generation plan using the plan service
            var plan = await _artworkGenerationPlanService.BuildPlanAsync(cp.ProjectId, cp.CollectionId, artworkItemId, resolutionTier: 2);

            ImageGenerationModel? model = null;
            if (modelId > 0)
                model = await _imageGenerationModelRepository.GetByIdAsync(modelId);
            else if (!string.IsNullOrWhiteSpace(plan.Artwork.ImageModel))
                model = await _imageGenerationModelRepository.GetByModelKeyAsync(plan.Artwork.ImageModel);

            if (model == null)
                return Json(new { success = false, message = "Image model not found." });

            var estImageGen = _imageGenerations.FirstOrDefault(g => g.ModelKey.Equals(model.ModelKey, StringComparison.OrdinalIgnoreCase));
            if (estImageGen == null)
                return Json(new { success = false, message = "Image model not supported." });

            var tokenizer = estImageGen.CreateTokenizer(model);
            var cost = _tokenCostOptions.Cost > 0 ? _tokenCostOptions.Cost : 0.01m;

            // Use reference image dimensions from the plan
            var inputImageDimensions = plan.ReferenceImages
                .Where(r => r.Width > 0 && r.Height > 0)
                .Select(r => (r.Width, r.Height))
                .ToList() as IReadOnlyList<(int width, int height)>;

            // Sum tokens across all tasks (variants)
            var totalTokens = 0m;
            foreach (var task in plan.Tasks)
            {
                var result = tokenizer.CalculateTokens(
                    plan.FinalPrompt,
                    task.Width,
                    task.Height,
                    "medium",
                    inputImageDimensions,
                    "auto",
                    cost);
                totalTokens += result.PlatformTokens;
            }

            return Json(new { success = true, data = (int)Math.Ceiling(totalTokens) });
        }

        [HttpPost("{orderId}/items/{orderItemId}/generate-artwork")]
        public async Task<IActionResult> GenerateArtwork(Guid orderId, Guid orderItemId, [FromBody] GenerateOrderItemArtworkRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            if (request.OrderId != orderId || request.OrderItemId != orderItemId)
                return Json(new { success = false, message = "Order ID mismatch." });

            if (request.ArtworkItemId == Guid.Empty)
                return Json(new { success = false, message = "Artwork item ID is required." });

            if (request.ModelId <= 0)
                return Json(new { success = false, message = "Image model is required." });

            var order = await _orderRepository.GetByIdWithDetailsAsync(orderId);
            if (order == null || order.Order.AppUserId != userId)
                return NotFound();

            var item = order.Items.FirstOrDefault(i => i.Id == orderItemId);
            if (item == null)
                return NotFound();

            var cp = await _projectCollectionProductRepository.GetByIdAsync(item.CollectionProductId);
            if (cp == null)
                return Json(new { success = false, message = "Collection product not found." });

            var projectItem = await _projectItemRepository.GetByIdAsync(request.ArtworkItemId);
            if (projectItem == null || projectItem.ProjectId != cp.ProjectId)
                return Json(new { success = false, message = "Project item not found." });

            var genModel = await _imageGenerationModelRepository.GetByIdAsync(request.ModelId);
            if (genModel == null)
                return Json(new { success = false, message = "Image model not found." });

            // Build answers from saved order item answers
            var savedAnswers = (await _orderItemAnswerRepository.GetByOrderItemIdAsync(orderItemId)).ToList();
            var answers = savedAnswers.Select(a => new GenerateProjectItemPreviewAnswer
            {
                QuestionId = a.QuestionId,
                Answer = a.Answer ?? ""
            }).ToList();

            // Build the generation plan using the plan service
            var plan = await _artworkGenerationPlanService.BuildPlanAsync(
                cp.ProjectId, cp.CollectionId, request.ArtworkItemId,
                request.RequestText, answers, resolutionTier: 2);

            if (string.IsNullOrWhiteSpace(plan.FinalPrompt))
                return Json(new { success = false, message = "Prompt is required to generate artwork." });

            var imageGen = _imageGenerations.FirstOrDefault(g => g.ModelKey.Equals(genModel.ModelKey, StringComparison.OrdinalIgnoreCase));
            if (imageGen == null)
                return Json(new { success = false, message = "Image model not supported." });

            var inputImages = plan.ReferenceImages.Select(r => r.ImageBytes).Where(b => b != null && b.Length > 0).ToList()!;
            var totalPlacements = plan.Tasks.Count;
            var generatedArtworks = new List<object>();

            // Deactivate existing artworks for this item (regeneration replaces them)
            var existingArtworks = (await _orderItemArtworkRepository.GetByOrderItemIdAsync(orderItemId))
                .Where(a => a.ItemId == request.ArtworkItemId).ToList();
            foreach (var existing in existingArtworks)
            {
                existing.Active = false;
                existing.Updated = DateTime.UtcNow;
                await _orderItemArtworkRepository.UpdateAsync(existing);
            }

            // Generate one artwork per variant task
            for (var i = 0; i < plan.Tasks.Count; i++)
            {
                var task = plan.Tasks[i];

                var genRequest = new ImageGenerationRequest
                {
                    Model = genModel.Model,
                    Prompt = plan.FinalPrompt,
                    InputImages = inputImages,
                    Width = task.Width,
                    Height = task.Height,
                    Quality = "medium",
                    UseResponsesApi = false
                };

                var generated = await imageGen.GenerateAsync(genRequest);
                if (generated.ImageBytes == null || generated.ImageBytes.Length == 0)
                    return Json(new { success = false, message = "Image generation failed." });

                var orderItemArtwork = new OrderItemArtwork
                {
                    OrderId = orderId,
                    OrderItemId = orderItemId,
                    ProjectId = cp.ProjectId,
                    CollectionId = cp.CollectionId,
                    ItemId = request.ArtworkItemId,
                    Active = true,
                    Width = task.Width,
                    Height = task.Height,
                    ImageModel = genModel.Model,
                    Prompt = plan.FinalPrompt,
                    RequestText = request.RequestText ?? "",
                    Accepted = false,
                    FullSize = false,
                    Index = projectItem.Index,
                    ResponseId = generated.ResponseId ?? "",
                    PlacementIndex = task.VariantIndex,
                    TotalPlacements = totalPlacements
                };
                var created = await _orderItemArtworkRepository.CreateAsync(orderItemArtwork);

                // Save the main artwork image
                await _imageService.SaveOrderItemArtworkAsync(cp.ProjectId, cp.CollectionId, orderId, created.Id, generated.ImageBytes);

                // --- Seamless placement group: cut up the generated image ---
                if (task.GroupId.HasValue)
                {
                    var groupId = task.GroupId.Value;
                    byte[] imageToCut = generated.ImageBytes;

                    // Apply opacity if needed
                    if (plan.HasOpacity)
                    {
                        var opacitySettings = _opacityService.ParseOpacityJson(plan.Artwork.OpacityJson);
                        if (opacitySettings != null && opacitySettings.ChromaKeys.Count > 0)
                        {
                            var pngBytes = await _opacityService.ApplyChromaKeysAsync(generated.ImageBytes, opacitySettings);
                            if (opacitySettings.Overlay != null && !string.IsNullOrWhiteSpace(opacitySettings.Overlay.Color))
                                pngBytes = await _opacityService.ApplyOverlayAsync(pngBytes, opacitySettings.Overlay.Color);

                            await _imageService.SaveOrderItemArtworkPngAsync(cp.ProjectId, cp.CollectionId, orderId, created.Id, pngBytes);
                            imageToCut = pngBytes;
                            created.Opacity = true;
                            await _orderItemArtworkRepository.UpdateAsync(created);
                        }
                    }

                    // Cut the image vertically into segments
                    var segmentHeights = task.GroupPlacements.Select(p => p.Height).ToList();
                    var segments = await _imageService.CutImageVerticalAsync(imageToCut, segmentHeights);

                    // Save each segment to the order group folder
                    for (var segIdx = 0; segIdx < segments.Count && segIdx < task.GroupPlacements.Count; segIdx++)
                    {
                        var placement = task.GroupPlacements[segIdx];
                        var segBytes = segments[segIdx];

                        // Apply flips: FlipX = top/bottom mirror, FlipY = left/right mirror
                        if (placement.FlipX)
                            segBytes = await _imageService.MirrorXAsync(segBytes);
                        if (placement.FlipY)
                            segBytes = await _imageService.MirrorYAsync(segBytes);

                        if (created.Opacity)
                            await _imageService.SaveOrderItemArtworkGroupImagePngAsync(cp.ProjectId, cp.CollectionId, orderId, created.Id, groupId, placement.Position, segBytes);
                        else
                            await _imageService.SaveOrderItemArtworkGroupImageAsync(cp.ProjectId, cp.CollectionId, orderId, created.Id, groupId, placement.Position, segBytes);

                        // Save placement record with group info
                        var placementRecord = new OrderItemArtworkPlacement
                        {
                            OrderItemArtworkId = created.Id,
                            Width = placement.Width,
                            Height = placement.Height,
                            Index = segIdx,
                            ResponseId = generated.ResponseId ?? "",
                            GroupId = groupId,
                            Position = placement.Position
                        };
                        await _orderItemArtworkPlacementRepository.CreateAsync(placementRecord);
                    }
                }
                else
                {
                    // Standard placement variant — save as placement-specific image
                    await _imageService.SaveOrderItemArtworkPlacementAsync(cp.ProjectId, cp.CollectionId, orderId, created.Id, i, generated.ImageBytes);

                    // Apply opacity/chroma key if needed
                    if (plan.HasOpacity)
                    {
                        var opacitySettings = _opacityService.ParseOpacityJson(plan.Artwork.OpacityJson);
                        if (opacitySettings != null && opacitySettings.ChromaKeys.Count > 0)
                        {
                            var pngBytes = await _opacityService.ApplyChromaKeysAsync(generated.ImageBytes, opacitySettings);
                            if (opacitySettings.Overlay != null && !string.IsNullOrWhiteSpace(opacitySettings.Overlay.Color))
                                pngBytes = await _opacityService.ApplyOverlayAsync(pngBytes, opacitySettings.Overlay.Color);

                            await _imageService.SaveOrderItemArtworkPngAsync(cp.ProjectId, cp.CollectionId, orderId, created.Id, pngBytes);
                            await _imageService.SaveOrderItemArtworkPlacementPngAsync(cp.ProjectId, cp.CollectionId, orderId, created.Id, i, pngBytes);

                            created.Opacity = true;
                            await _orderItemArtworkRepository.UpdateAsync(created);
                        }
                    }

                    // Save the placement record with placement dimensions
                    var placement = new OrderItemArtworkPlacement
                    {
                        OrderItemArtworkId = created.Id,
                        Width = task.PlacementWidth,
                        Height = task.PlacementHeight,
                        Index = i,
                        ResponseId = generated.ResponseId ?? ""
                    };
                    await _orderItemArtworkPlacementRepository.CreateAsync(placement);
                }

                var imageUrl = $"/api/orders/order-items/{orderItemId}/artworks/{created.Id}";
                generatedArtworks.Add(new
                {
                    id = created.Id,
                    url = imageUrl,
                    width = created.Width,
                    height = created.Height,
                    prompt = created.Prompt,
                    placementIndex = created.PlacementIndex,
                    totalPlacements = created.TotalPlacements,
                    placementWidth = task.PlacementWidth,
                    placementHeight = task.PlacementHeight
                });
            }

            return Json(new
            {
                success = true,
                data = new
                {
                    artworks = generatedArtworks,
                    artwork = generatedArtworks.FirstOrDefault()
                }
            });
        }

        [HttpPost("{orderId}/items/{orderItemId}/artworks/{artworkId}/accept")]
        public async Task<IActionResult> AcceptOrderItemArtwork(Guid orderId, Guid orderItemId, Guid artworkId)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var order = await _orderRepository.GetByIdWithDetailsAsync(orderId);
            if (order == null || order.Order.AppUserId != userId)
                return NotFound();

            var item = order.Items.FirstOrDefault(i => i.Id == orderItemId);
            if (item == null)
                return NotFound();

            var artwork = await _orderItemArtworkRepository.GetByIdAsync(artworkId);
            if (artwork == null || artwork.OrderItemId != item.Id)
                return NotFound();

            artwork.Accepted = true;
            artwork.Updated = DateTime.UtcNow;
            await _orderItemArtworkRepository.UpdateAsync(artwork);

            return Json(new { success = true });
        }

        [HttpGet("{orderId}/items/{orderItemId}/artworks")]
        public async Task<IActionResult> GetOrderItemArtworks(Guid orderId, Guid orderItemId)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var order = await _orderRepository.GetByIdWithDetailsAsync(orderId);
            if (order == null || order.Order.AppUserId != userId)
                return NotFound();

            var item = order.Items.FirstOrDefault(i => i.Id == orderItemId);
            if (item == null)
                return NotFound();

            var artworks = await _orderItemArtworkRepository.GetByOrderItemIdAsync(item.Id);
            var result = new List<object>();
            foreach (var a in artworks)
            {
                var placements = await _orderItemArtworkPlacementRepository.GetByArtworkIdAsync(a.Id);
                result.Add(new
                {
                    a.Id,
                    a.ItemId,
                    a.Accepted,
                    a.Width,
                    a.Height,
                    a.ImageModel,
                    a.Prompt,
                    a.RequestText,
                    a.Opacity,
                    a.PlacementIndex,
                    a.TotalPlacements,
                    Placements = placements.Select(p => new { p.Id, p.Width, p.Height, p.Index, p.GroupId, p.Position }),
                    a.Created,
                    a.Updated
                });
            }

            return Json(new { success = true, data = result });
        }

        [HttpGet("{orderId}/items/{orderItemId}/download-zip")]
        public async Task<IActionResult> DownloadOrderItemArtworks(Guid orderId, Guid orderItemId)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var order = await _orderRepository.GetByIdWithDetailsAsync(orderId);
            if (order == null || order.Order.AppUserId != userId)
                return NotFound();

            var item = order.Items.FirstOrDefault(i => i.Id == orderItemId);
            if (item == null)
                return NotFound();

            var artworks = await _orderItemArtworkRepository.GetByOrderItemIdAsync(item.Id);
            using var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                var index = 1;
                foreach (var a in artworks)
                {
                    var placements = await _orderItemArtworkPlacementRepository.GetByArtworkIdAsync(a.Id);
                    var placementList = placements.ToList();

                    // Group placements (seamless groups) — use placement-level GroupId/Position
                    var groupPlacements = placementList.Where(p => p.GroupId.HasValue).ToList();
                    var standardPlacements = placementList.Where(p => !p.GroupId.HasValue).ToList();

                    foreach (var p in groupPlacements)
                    {
                        if (string.IsNullOrWhiteSpace(p.Position)) continue;
                        var groupId = p.GroupId!.Value;

                        byte[]? bytes = null;
                        var ext = "jpg";

                        if (a.Opacity)
                        {
                            bytes = await _imageService.GetOrderItemArtworkGroupImagePngAsync(a.ProjectId, a.CollectionId, a.OrderId, a.Id, groupId, p.Position);
                            ext = "png";
                        }

                        if (bytes == null || bytes.Length == 0)
                        {
                            bytes = await _imageService.GetOrderItemArtworkGroupImageAsync(a.ProjectId, a.CollectionId, a.OrderId, a.Id, groupId, p.Position);
                            ext = "jpg";
                        }

                        if (bytes == null || bytes.Length == 0)
                            continue;

                        var entry = zip.CreateEntry($"artwork_{index}_{p.Position}.{ext}");
                        using var es = entry.Open();
                        await es.WriteAsync(bytes);
                        index++;
                    }

                    if (groupPlacements.Count > 0 && standardPlacements.Count == 0)
                        continue;

                    // Standard artwork
                    byte[]? stdBytes = null;
                    var stdExt = "jpg";

                    // Try placement-specific image first
                    if (a.Opacity)
                    {
                        var pngBytes = await _imageService.GetOrderItemArtworkPlacementPngAsync(a.ProjectId, a.CollectionId, a.OrderId, a.Id, a.PlacementIndex);
                        if (pngBytes == null || pngBytes.Length == 0)
                            pngBytes = await _imageService.GetOrderItemArtworkPngAsync(a.ProjectId, a.CollectionId, a.OrderId, a.Id);
                        if (pngBytes != null && pngBytes.Length > 0)
                        {
                            stdBytes = pngBytes;
                            stdExt = "png";
                        }
                    }

                    if (stdBytes == null || stdBytes.Length == 0)
                    {
                        stdBytes = await _imageService.GetOrderItemArtworkPlacementImageAsync(a.ProjectId, a.CollectionId, a.OrderId, a.Id, a.PlacementIndex);
                        if (stdBytes == null || stdBytes.Length == 0)
                            stdBytes = await _imageService.GetOrderItemArtworkImageAsync(a.ProjectId, a.CollectionId, a.OrderId, a.Id);
                        stdExt = "jpg";
                    }

                    if (stdBytes == null || stdBytes.Length == 0)
                        continue;

                    var stdEntry = zip.CreateEntry($"artwork_{index}.{stdExt}");
                    using var stdEs = stdEntry.Open();
                    await stdEs.WriteAsync(stdBytes);
                    index++;
                }
            }

            var zipBytes = ms.ToArray();
            var fileName = string.IsNullOrWhiteSpace(order.Order.OrderId)
                ? $"order_{order.Order.Id}_artworks.zip"
                : $"order_{order.Order.OrderId}_artworks.zip";
            return File(zipBytes, "application/zip", fileName);
        }
    }
}
