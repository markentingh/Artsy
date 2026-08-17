using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Artsy.API.Models;
using Artsy.API.Models.Collections;
using Artsy.API.Services;
using Artsy.Data.Entities;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces;
using Artsy.Data.Interfaces.Orders;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.API.Controllers
{
    [Authorize]
    [Route("/api/orders")]
    public class OrdersController : ApiController
    {
        readonly IOrderRepository _orderRepository;
        readonly IProjectCollectionProductImageRepository _productImageRepository;
        readonly IProjectCollectionProductRepository _projectCollectionProductRepository;
        readonly IProjectCollectionProductPlacementRepository _placementRepository;
        readonly IProjectCollectionArtworkRepository _collectionArtworkRepository;
        readonly IProjectBlueprintsRepository _projectBlueprintsRepository;
        readonly IProjectItemRepository _projectItemRepository;
        readonly IProjectItemArtworkRepository _projectItemArtworkRepository;
        readonly IProjectItemReferenceRepository _projectItemReferenceRepository;
        readonly IPrintifyBlueprintVariantPlaceholderRepository _placeholderRepository;
        readonly IOrderItemArtworkRepository _orderItemArtworkRepository;
        readonly IImageGenerationModelRepository _imageGenerationModelRepository;
        readonly IEnumerable<IImageGeneration> _imageGenerations;
        readonly IPrintifyOrders _printifyOrders;
        readonly TokenCostOptions _tokenCostOptions;

        public OrdersController(IOrderRepository orderRepository, IProjectCollectionProductImageRepository productImageRepository, IProjectCollectionProductRepository projectCollectionProductRepository, IProjectCollectionProductPlacementRepository placementRepository, IProjectCollectionArtworkRepository collectionArtworkRepository, IProjectBlueprintsRepository projectBlueprintsRepository, IProjectItemRepository projectItemRepository, IProjectItemArtworkRepository projectItemArtworkRepository, IProjectItemReferenceRepository projectItemReferenceRepository, IPrintifyBlueprintVariantPlaceholderRepository placeholderRepository, IOrderItemArtworkRepository orderItemArtworkRepository, IImageGenerationModelRepository imageGenerationModelRepository, IEnumerable<IImageGeneration> imageGenerations, IPrintifyOrders printifyOrders, IOptions<TokenCostOptions> tokenCostOptions)
        {
            _orderRepository = orderRepository;
            _productImageRepository = productImageRepository;
            _projectCollectionProductRepository = projectCollectionProductRepository;
            _placementRepository = placementRepository;
            _collectionArtworkRepository = collectionArtworkRepository;
            _projectBlueprintsRepository = projectBlueprintsRepository;
            _projectItemRepository = projectItemRepository;
            _projectItemArtworkRepository = projectItemArtworkRepository;
            _projectItemReferenceRepository = projectItemReferenceRepository;
            _placeholderRepository = placeholderRepository;
            _orderItemArtworkRepository = orderItemArtworkRepository;
            _imageGenerationModelRepository = imageGenerationModelRepository;
            _imageGenerations = imageGenerations;
            _printifyOrders = printifyOrders;
            _tokenCostOptions = tokenCostOptions.Value;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var orders = await _orderRepository.GetByUserWithDetailsAsync(userId);
            return Json(new { success = true, data = orders });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var order = await _orderRepository.GetByIdWithDetailsAsync(id);
            if (order == null || order.Order.AppUserId != userId)
                return NotFound();

            return Json(new { success = true, data = order });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh()
        {                               
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var (newCount, updatedCount) = await _printifyOrders.RefreshForUserAsync(userId);
            return Json(new { success = true, newOrders = newCount, updatedOrders = updatedCount });
        }

        [HttpGet("{orderId}/images")]
        public async Task<IActionResult> GetOrderImages(Guid orderId)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var order = await _orderRepository.GetByIdWithDetailsAsync(orderId);
            if (order == null || order.Order.AppUserId != userId)
                return NotFound();

            var domain = ConnectionSettings.MetaImagesDomain?.TrimEnd('/') ?? "";
            var result = new Dictionary<string, List<string>>();

            foreach (var item in order.Items)
            {
                var urls = new List<string>();

                if (item.CollectionProductId != Guid.Empty)
                {
                    var cp = await _projectCollectionProductRepository.GetByIdAsync(item.CollectionProductId);
                    if (cp != null)
                    {
                        var productImages = await _productImageRepository.GetByCollectionAndBlueprintIdAsync(cp.CollectionId, cp.ProjectBlueprintId);
                        urls.AddRange(productImages.Select(i => $"{domain}/meta/image/product/{i.Id}?thumb=true"));
                    }
                }

                var orderArtworks = await _orderItemArtworkRepository.GetByOrderItemIdAsync(item.Id);
                foreach (var artwork in orderArtworks)
                {
                    urls.Insert(0, $"{domain}/api/orders/order-items/{item.Id}/artworks/{artwork.Id}");
                }

                result[item.Id.ToString()] = urls;
            }

            return Json(new { success = true, images = result });
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
            var projectItems = (await _projectItemRepository.GetByProjectIdAsync(cp.ProjectId)).ToDictionary(pi => pi.Id, pi => pi.Title ?? "");

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
                    return (object)new
                    {
                        p.Id,
                        p.Position,
                        p.ArtworkId,
                        artworkItemId = artwork?.ItemId,
                        artworkItemTitle = artwork != null && projectItems.TryGetValue(artwork.ItemId, out var title) ? title : (string?)null,
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
                    var placementDtos = (System.Text.Json.JsonSerializer.Deserialize<List<PlacementDto>>(bp.PlacementJson) ?? new List<PlacementDto>())
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
                        return (object)new
                        {
                            Id = Guid.NewGuid(),
                            Position = p.Position,
                            Source = p.Source,
                            ArtworkId = artwork?.Id ?? Guid.Empty,
                            artworkItemId = artwork?.ItemId,
                            artworkItemTitle = artwork != null && projectItems.TryGetValue(artwork.ItemId, out var title) ? title : (string?)null,
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

            var allPlaceholders = (await _placeholderRepository.GetByVariantIdAsync(item.VariantId)).ToList();
            var allCollectionArtwork = (await _collectionArtworkRepository.GetByCollectionIdAsync(cp.CollectionId)).ToList();

            int maxWidth = 0;
            int maxHeight = 0;

            var placements = (await _placementRepository.GetByProductIdAndVariantIdAsync(cp.Id, item.VariantId))
                .Where(p => p.ArtworkId != Guid.Empty)
                .ToList();

            if (placements.Count > 0)
            {
                var artworkIds = placements.Select(p => p.ArtworkId).Distinct().ToList();
                var artworks = allCollectionArtwork
                    .Where(a => artworkIds.Contains(a.Id))
                    .ToDictionary(a => a.Id);

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
                    var placementDtos = (System.Text.Json.JsonSerializer.Deserialize<List<PlacementDto>>(bp.PlacementJson) ?? new List<PlacementDto>())
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
                return Json(new { success = false, message = "No dimensions found for the artwork." });

            var resolution = ImageGenerationForOpenAI.FindBestResolution($"{maxWidth}x{maxHeight}");
            var parts = resolution.Split('x');
            if (parts.Length != 2 || !int.TryParse(parts[0], out var w) || !int.TryParse(parts[1], out var h))
            {
                w = 1024;
                h = 1024;
            }

            var itemArtworkList = await _projectItemArtworkRepository.GetByItemIdAsync(artworkItemId);
            var itemArtwork = itemArtworkList.FirstOrDefault();
            if (itemArtwork == null)
                return Json(new { success = false, message = "Project item artwork not found." });

            ImageGenerationModel? model = null;
            if (modelId > 0)
            {
                model = await _imageGenerationModelRepository.GetByIdAsync(modelId);
            }
            else if (!string.IsNullOrWhiteSpace(itemArtwork.ImageModel))
            {
                model = await _imageGenerationModelRepository.GetByModelKeyAsync(itemArtwork.ImageModel);
            }

            if (model == null)
                return Json(new { success = false, message = "Image model not found." });

            var references = await _projectItemReferenceRepository.GetByItemIdAsync(artworkItemId);
            var inputImages = references.Select(r => (1024, 1024)).ToList() as IReadOnlyList<(int width, int height)>;

            var estImageGen = _imageGenerations.FirstOrDefault(g => g.ModelKey.Equals(model.ModelKey, StringComparison.OrdinalIgnoreCase));
            if (estImageGen == null)
                return Json(new { success = false, message = "Image model not supported." });

            var tokenizer = estImageGen.CreateTokenizer(model);
            var cost = _tokenCostOptions.Cost > 0 ? _tokenCostOptions.Cost : 0.01m;
            var result = tokenizer.CalculateTokens(
                itemArtwork.Prompt ?? "",
                w,
                h,
                "medium",
                inputImages,
                "auto",
                cost
            );

            return Json(new { success = true, data = result.PlatformTokens });
        }
    }
}
