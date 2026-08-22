using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using Artsy.API.Services;
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
        readonly IOrderItemArtworkRepository _orderItemArtworkRepository;
        readonly IPrintifyBlueprintVariantRepository _printifyBlueprintVariantRepository;
        readonly IImageService _imageService;
        readonly IPrintifyOrders _printifyOrders;

        public OrdersController(IOrderRepository orderRepository, IProjectCollectionProductImageRepository productImageRepository, IProjectCollectionProductRepository projectCollectionProductRepository, IOrderItemArtworkRepository orderItemArtworkRepository, IPrintifyBlueprintVariantRepository printifyBlueprintVariantRepository, IImageService imageService, IPrintifyOrders printifyOrders)
        {
            _orderRepository = orderRepository;
            _productImageRepository = productImageRepository;
            _projectCollectionProductRepository = projectCollectionProductRepository;
            _orderItemArtworkRepository = orderItemArtworkRepository;
            _printifyBlueprintVariantRepository = printifyBlueprintVariantRepository;
            _imageService = imageService;
            _printifyOrders = printifyOrders;
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

        [HttpGet("{orderId}/artworks")]
        public async Task<IActionResult> GetOrderArtworks(Guid orderId)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var order = await _orderRepository.GetByIdWithDetailsAsync(orderId);
            if (order == null || order.Order.AppUserId != userId)
                return NotFound();

            var result = new Dictionary<string, bool>();
            foreach (var item in order.Items)
            {
                var artworks = await _orderItemArtworkRepository.GetByOrderItemIdAsync(item.Id);
                result[item.Id.ToString()] = artworks.Any();
            }

            return Json(new { success = true, hasArtworks = result });
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

                var orderArtworks = await _orderItemArtworkRepository.GetByOrderItemIdAsync(item.Id);
                urls.AddRange(orderArtworks.OrderBy(a => a.Index).Select(a => $"/api/orders/order-items/{item.Id}/artworks/{a.Id}"));

                if (item.CollectionProductId != Guid.Empty)
                {
                    var cp = await _projectCollectionProductRepository.GetByIdAsync(item.CollectionProductId);
                    if (cp != null)
                    {
                        var productImages = (await _productImageRepository.GetByCollectionAndBlueprintIdAsync(cp.CollectionId, cp.ProjectBlueprintId)).ToList();
                        if (item.VariantId > 0)
                        {
                            var variants = await _printifyBlueprintVariantRepository.GetByBlueprintAndProviderAsync(cp.BlueprintId, item.PrintProviderId);
                            var variant = variants.FirstOrDefault(v => v.VariantId == item.VariantId);
                            if (variant != null)
                            {
                                productImages = productImages.Where(i => i.VariantColor == variant.Color).ToList();
                            }
                        }
                        urls.AddRange(productImages.Select(i => $"{domain}/meta/image/product/{i.Id}?thumb=true"));
                    }
                }

                result[item.Id.ToString()] = urls;
            }

            return Json(new { success = true, images = result });
        }

        [AllowAnonymous]
        [HttpGet("order-items/{orderItemId}/artworks/{artworkId}")]
        public async Task<IActionResult> GetOrderItemArtwork(Guid orderItemId, Guid artworkId, [FromQuery] int? placementIndex)
        {
            var artwork = await _orderItemArtworkRepository.GetByIdAsync(artworkId);
            if (artwork == null || artwork.OrderItemId != orderItemId)
                return NotFound();

            byte[]? imgBytes = null;

            // If placementIndex is specified, serve the placement-specific image
            if (placementIndex.HasValue && placementIndex.Value >= 0)
            {
                if (artwork.Opacity)
                {
                    imgBytes = await _imageService.GetOrderItemArtworkPlacementPngAsync(artwork.ProjectId, artwork.CollectionId, artwork.OrderId, artwork.Id, placementIndex.Value);
                    if (imgBytes == null || imgBytes.Length == 0)
                        imgBytes = await _imageService.GetOrderItemArtworkPngAsync(artwork.ProjectId, artwork.CollectionId, artwork.OrderId, artwork.Id);
                }

                if (imgBytes == null || imgBytes.Length == 0)
                {
                    imgBytes = await _imageService.GetOrderItemArtworkPlacementImageAsync(artwork.ProjectId, artwork.CollectionId, artwork.OrderId, artwork.Id, placementIndex.Value);
                    if (imgBytes == null || imgBytes.Length == 0)
                        imgBytes = await _imageService.GetOrderItemArtworkImageAsync(artwork.ProjectId, artwork.CollectionId, artwork.OrderId, artwork.Id);
                }
            }
            else
            {
                if (artwork.Opacity)
                {
                    imgBytes = await _imageService.GetOrderItemArtworkPngAsync(artwork.ProjectId, artwork.CollectionId, artwork.OrderId, artwork.Id);
                }

                if (imgBytes == null || imgBytes.Length == 0)
                    imgBytes = await _imageService.GetOrderItemArtworkImageAsync(artwork.ProjectId, artwork.CollectionId, artwork.OrderId, artwork.Id);
            }

            if (imgBytes == null || imgBytes.Length == 0)
                return NotFound();

            var contentType = artwork.Opacity ? "image/png" : "image/jpeg";
            var fileName = artwork.Opacity ? "artwork.png" : "artwork.jpg";
            Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileName}\"";
            Response.Headers["Cache-Control"] = "public, max-age=86400";
            return File(imgBytes, contentType);
        }

        [HttpGet("order-items/{orderItemId}/artworks/{artworkId}/group/{groupId}/{position}")]
        public async Task<IActionResult> GetOrderItemArtworkGroupImage(Guid orderItemId, Guid artworkId, Guid groupId, string position, [FromQuery] bool png = false)
        {
            var artwork = await _orderItemArtworkRepository.GetByIdAsync(artworkId);
            if (artwork == null || artwork.OrderItemId != orderItemId)
                return NotFound();

            byte[]? imgBytes = null;
            var contentType = "image/jpeg";

            if (png || artwork.Opacity)
            {
                contentType = "image/png";
                imgBytes = await _imageService.GetOrderItemArtworkGroupImagePngAsync(artwork.ProjectId, artwork.CollectionId, artwork.OrderId, artwork.Id, groupId, position);
            }

            if (imgBytes == null || imgBytes.Length == 0)
            {
                imgBytes = await _imageService.GetOrderItemArtworkGroupImageAsync(artwork.ProjectId, artwork.CollectionId, artwork.OrderId, artwork.Id, groupId, position);
            }

            if (imgBytes == null || imgBytes.Length == 0)
                return NotFound();

            Response.Headers["Content-Disposition"] = $"inline; filename=\"{position}.jpg\"";
            Response.Headers["Cache-Control"] = "public, max-age=86400";
            return File(imgBytes, contentType);
        }

    }
}
