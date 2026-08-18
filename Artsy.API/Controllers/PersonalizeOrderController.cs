using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Artsy.API.Models;
using Artsy.API.Models.Collections;
using Artsy.API.Models.Orders;
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
        readonly IImageGenerationModelRepository _imageGenerationModelRepository;
        readonly IEnumerable<IImageGeneration> _imageGenerations;
        readonly IImageService _imageService;
        readonly ICustomImageRepository _customImageRepository;
        readonly IOpacityService _opacityService;
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
            IImageGenerationModelRepository imageGenerationModelRepository,
            IEnumerable<IImageGeneration> imageGenerations,
            IImageService imageService,
            ICustomImageRepository customImageRepository,
            IOpacityService opacityService,
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
            _imageGenerationModelRepository = imageGenerationModelRepository;
            _imageGenerations = imageGenerations;
            _imageService = imageService;
            _customImageRepository = customImageRepository;
            _opacityService = opacityService;
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
                        artworkItemId = artwork?.ItemId,
                        artworkItemTitle = projectItem?.Title,
                        artworkItemIndex = projectItem?.Index ?? 0,
                        artworkImageModel = artwork?.ImageModel,
                        artworkPrompt = artwork?.Prompt,
                        artworkAccepted = artwork?.Accepted,
                        artworkFullSize = artwork?.FullSize,
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

            var (w, h) = await GetPlacementDimensionsAsync(cp, item.VariantId, artworkItemId);
            if (w <= 0 || h <= 0)
                return Json(new { success = false, message = "No dimensions found for the artwork." });

            var itemArtworkList = await _projectItemArtworkRepository.GetByItemIdAsync(artworkItemId);
            var itemArtwork = itemArtworkList.FirstOrDefault();
            if (itemArtwork == null)
                return Json(new { success = false, message = "Project item artwork not found." });

            ImageGenerationModel? model = null;
            if (modelId > 0)
                model = await _imageGenerationModelRepository.GetByIdAsync(modelId);
            else if (!string.IsNullOrWhiteSpace(itemArtwork.ImageModel))
                model = await _imageGenerationModelRepository.GetByModelKeyAsync(itemArtwork.ImageModel);

            if (model == null)
                return Json(new { success = false, message = "Image model not found." });

            var references = await _projectItemReferenceRepository.GetByItemIdAsync(artworkItemId);
            var inputImages = references.Select(r => (1024, 1024)).ToList() as IReadOnlyList<(int width, int height)>;

            var estImageGen = _imageGenerations.FirstOrDefault(g => g.ModelKey.Equals(model.ModelKey, StringComparison.OrdinalIgnoreCase));
            if (estImageGen == null)
                return Json(new { success = false, message = "Image model not supported." });

            var tokenizer = estImageGen.CreateTokenizer(model);
            var cost = _tokenCostOptions.Cost > 0 ? _tokenCostOptions.Cost : 0.01m;
            var result = tokenizer.CalculateTokens(itemArtwork.Prompt ?? "", w, h, "medium", inputImages, "auto", cost);

            return Json(new { success = true, data = result.PlatformTokens });
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

            var artworkList = await _projectItemArtworkRepository.GetByItemIdAsync(request.ArtworkItemId);
            var artwork = artworkList.FirstOrDefault();
            if (artwork == null || string.IsNullOrWhiteSpace(artwork.Prompt))
                return Json(new { success = false, message = "No prompt configured for this item." });

            var genModel = await _imageGenerationModelRepository.GetByIdAsync(request.ModelId);
            if (genModel == null)
                return Json(new { success = false, message = "Image model not found." });

            var promptBuilder = new StringBuilder(artwork.Prompt ?? "");

            var savedAnswers = (await _orderItemAnswerRepository.GetByOrderItemIdAsync(orderItemId)).ToList();
            if (savedAnswers.Any())
            {
                var projectQuestions = await _projectQuestionRepository.GetByProjectIdAsync(cp.ProjectId);
                var questionLookup = projectQuestions.ToDictionary(q => q.Id, q => q.Question);
                foreach (var answer in savedAnswers.Where(a => !string.IsNullOrWhiteSpace(a.Answer)))
                {
                    if (questionLookup.TryGetValue(answer.QuestionId, out var questionText))
                    {
                        promptBuilder.AppendLine();
                        promptBuilder.AppendLine($"Question: {questionText}");
                        promptBuilder.AppendLine($"Answer: {answer.Answer}");
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(request.RequestText))
            {
                promptBuilder.AppendLine();
                promptBuilder.AppendLine($"Requested Changes: {request.RequestText}");
            }

            var finalPrompt = promptBuilder.ToString().Trim();
            if (string.IsNullOrWhiteSpace(finalPrompt))
                return Json(new { success = false, message = "Prompt is required to generate artwork." });

            var opacitySettings = _opacityService.ParseOpacityJson(artwork.OpacityJson);
            if (opacitySettings != null && opacitySettings.ChromaKeys.Count > 0)
            {
                var firstColor = opacitySettings.ChromaKeys[0];
                var hexColor = $"#{firstColor.R:X2}{firstColor.G:X2}{firstColor.B:X2}";
                finalPrompt += $" the background for this image must be a completely flat, uniform, solid color using {hexColor} hex color with no gradients, textures, shadows, or objects, filling the entire background area, so that we can apply a chroma key to the image later";
            }

            var (w, h) = await GetPlacementDimensionsAsync(cp, item.VariantId, request.ArtworkItemId);
            if (w <= 0 || h <= 0)
            {
                w = 2048;
                h = 2048;
            }

            var references = await _projectItemReferenceRepository.GetByItemIdAsync(request.ArtworkItemId);
            var inputImages = new List<byte[]>();
            if (references != null && references.Any())
            {
                foreach (var reference in references)
                {
                    if (!reference.ArtworkId.HasValue)
                        continue;

                    var refCollectionArtwork = await _collectionArtworkRepository.GetByCollectionAndItemIdAsync(cp.CollectionId, reference.ArtworkId.Value);
                    if (refCollectionArtwork == null || !refCollectionArtwork.Active)
                        continue;

                    byte[]? imageBytes = null;
                    if (refCollectionArtwork.Opacity)
                    {
                        imageBytes = await _imageService.GetProjectCollectionArtworkPngAsync(cp.ProjectId, cp.CollectionId, reference.ArtworkId.Value, refCollectionArtwork.Id);
                        if (imageBytes == null || imageBytes.Length == 0)
                            imageBytes = await _imageService.GetProjectCollectionArtworkFullSizePngAsync(cp.ProjectId, cp.CollectionId, reference.ArtworkId.Value, refCollectionArtwork.Id);
                    }
                    else
                    {
                        imageBytes = await _imageService.GetProjectCollectionArtworkImageAsync(cp.ProjectId, cp.CollectionId, reference.ArtworkId.Value, refCollectionArtwork.Id);
                        if (imageBytes == null || imageBytes.Length == 0)
                            imageBytes = await _imageService.GetProjectCollectionArtworkFullSizeAsync(cp.ProjectId, cp.CollectionId, reference.ArtworkId.Value, refCollectionArtwork.Id);
                    }

                    if (imageBytes != null && imageBytes.Length > 0)
                        inputImages.Add(imageBytes);
                }
            }

            var imageGen = _imageGenerations.FirstOrDefault(g => g.ModelKey.Equals(genModel.ModelKey, StringComparison.OrdinalIgnoreCase));
            if (imageGen == null)
                return Json(new { success = false, message = "Image model not supported." });

            var genRequest = new ImageGenerationRequest
            {
                Model = genModel.Model,
                Prompt = finalPrompt,
                InputImages = inputImages,
                Width = w,
                Height = h,
                Quality = "medium",
                UseResponsesApi = false
            };

            var generated = await imageGen.GenerateAsync(genRequest);
            if (generated.ImageBytes == null || generated.ImageBytes.Length == 0)
                return Json(new { success = false, message = "Image generation failed." });

            var existingArtwork = (await _orderItemArtworkRepository.GetByOrderItemIdAsync(orderItemId))
                .FirstOrDefault(a => a.ItemId == request.ArtworkItemId);

            OrderItemArtwork created;
            if (existingArtwork != null)
            {
                created = existingArtwork;
                created.Active = true;
                created.Width = w;
                created.Height = h;
                created.ImageModel = genModel.Model;
                created.Prompt = finalPrompt;
                created.RequestText = request.RequestText ?? "";
                created.Accepted = false;
                created.FullSize = false;
                created.Index = projectItem.Index;
                created.ResponseId = generated.ResponseId ?? "";
                created.Opacity = false;
                created.Updated = DateTime.UtcNow;
            }
            else
            {
                var orderItemArtwork = new OrderItemArtwork
                {
                    OrderId = orderId,
                    OrderItemId = orderItemId,
                    ProjectId = cp.ProjectId,
                    CollectionId = cp.CollectionId,
                    ItemId = request.ArtworkItemId,
                    Active = true,
                    Width = w,
                    Height = h,
                    ImageModel = genModel.Model,
                    Prompt = finalPrompt,
                    RequestText = request.RequestText ?? "",
                    Accepted = false,
                    FullSize = false,
                    Index = projectItem.Index,
                    ResponseId = generated.ResponseId ?? ""
                };
                created = await _orderItemArtworkRepository.CreateAsync(orderItemArtwork);
            }

            await _imageService.SaveOrderItemArtworkAsync(cp.ProjectId, cp.CollectionId, orderId, created.Id, generated.ImageBytes);

            if (opacitySettings != null && opacitySettings.ChromaKeys.Count > 0)
            {
                var pngBytes = await _opacityService.ApplyChromaKeysAsync(generated.ImageBytes, opacitySettings);
                if (opacitySettings.Overlay != null && !string.IsNullOrWhiteSpace(opacitySettings.Overlay.Color))
                    pngBytes = await _opacityService.ApplyOverlayAsync(pngBytes, opacitySettings.Overlay.Color);

                await _imageService.SaveOrderItemArtworkPngAsync(cp.ProjectId, cp.CollectionId, orderId, created.Id, pngBytes);

                created.Opacity = true;
            }

            created.Active = true;
            created.ResponseId = generated.ResponseId ?? "";
            await _orderItemArtworkRepository.UpdateAsync(created);

            var imageUrl = $"/api/orders/order-items/{orderItemId}/artworks/{created.Id}";

            return Json(new
            {
                success = true,
                data = new
                {
                    artwork = new
                    {
                        id = created.Id,
                        url = imageUrl,
                        width = created.Width,
                        height = created.Height,
                        prompt = created.Prompt
                    }
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
            var result = artworks.Select(a => new
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
                a.Created,
                a.Updated
            });

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
                    byte[]? bytes = null;
                    var ext = "jpg";
                    if (a.Opacity)
                    {
                        var pngBytes = await _imageService.GetOrderItemArtworkPngAsync(a.ProjectId, a.CollectionId, a.OrderId, a.Id);
                        if (pngBytes != null && pngBytes.Length > 0)
                        {
                            bytes = pngBytes;
                            ext = "png";
                        }
                    }

                    if (bytes == null || bytes.Length == 0)
                    {
                        bytes = await _imageService.GetOrderItemArtworkImageAsync(a.ProjectId, a.CollectionId, a.OrderId, a.Id);
                        ext = "jpg";
                    }

                    if (bytes == null || bytes.Length == 0)
                        continue;

                    var entry = zip.CreateEntry($"artwork_{index}.{ext}");
                    using var es = entry.Open();
                    await es.WriteAsync(bytes);
                    index++;
                }
            }

            var zipBytes = ms.ToArray();
            var fileName = string.IsNullOrWhiteSpace(order.Order.OrderId)
                ? $"order_{order.Order.Id}_artworks.zip"
                : $"order_{order.Order.OrderId}_artworks.zip";
            return File(zipBytes, "application/zip", fileName);
        }

        async Task<(int Width, int Height)> GetPlacementDimensionsAsync(ProjectCollectionProduct cp, int variantId, Guid artworkItemId)
        {
            var allPlaceholders = (await _placeholderRepository.GetByVariantIdAsync(variantId)).ToList();
            int maxWidth = 0;
            int maxHeight = 0;

            var placements = (await _placementRepository.GetByProductIdAndVariantIdAsync(cp.Id, variantId))
                .Where(p => p.ArtworkId != Guid.Empty)
                .ToList();

            if (placements.Count > 0)
            {
                var allCollectionArtwork = (await _collectionArtworkRepository.GetByCollectionIdAsync(cp.CollectionId)).ToList();
                var artworkIds = placements.Select(p => p.ArtworkId).Distinct().ToList();
                var artworks = allCollectionArtwork.Where(a => artworkIds.Contains(a.Id)).ToDictionary(a => a.Id);

                foreach (var p in placements)
                {
                    if (!artworks.TryGetValue(p.ArtworkId, out var artwork) || artwork.ItemId != artworkItemId)
                        continue;

                    var placeholder = allPlaceholders.FirstOrDefault(ph => ph.Position == p.Position);
                    if (placeholder != null)
                    {
                        maxWidth = Math.Max(maxWidth, placeholder.Width);
                        maxHeight = Math.Max(maxHeight, placeholder.Height);
                    }
                }
            }
            else
            {
                var bp = await _projectBlueprintsRepository.GetByIdAsync(cp.ProjectBlueprintId);
                if (bp != null && !string.IsNullOrWhiteSpace(bp.PlacementJson))
                {
                    var placementDtos = (JsonSerializer.Deserialize<List<PlacementDto>>(bp.PlacementJson) ?? new List<PlacementDto>())
                        .Where(p => !string.Equals(p.Source, "custom", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    foreach (var p in placementDtos)
                    {
                        var itemId = p.GetItemId();
                        if (itemId != artworkItemId)
                            continue;

                        var (pw, ph) = p.GetDimensions();
                        var placeholder = allPlaceholders.FirstOrDefault(ph => ph.Position == p.Position);
                        maxWidth = Math.Max(maxWidth, placeholder?.Width ?? pw);
                        maxHeight = Math.Max(maxHeight, placeholder?.Height ?? ph);
                    }
                }
            }

            if (maxWidth <= 0 || maxHeight <= 0)
                return (0, 0);

            var resolution = ImageGenerationForOpenAI.FindBestResolution($"{maxWidth}x{maxHeight}");
            var parts = resolution.Split('x');
            if (parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h))
                return (w, h);

            return (1024, 1024);
        }
    }
}
