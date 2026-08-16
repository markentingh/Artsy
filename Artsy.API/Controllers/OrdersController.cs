using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Artsy.API.Services;
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
        readonly IPrintifyOrders _printifyOrders;

        public OrdersController(IOrderRepository orderRepository, IProjectCollectionProductImageRepository productImageRepository, IProjectCollectionProductRepository projectCollectionProductRepository, IOrderItemArtworkRepository orderItemArtworkRepository, IPrintifyOrders printifyOrders)
        {
            _orderRepository = orderRepository;
            _productImageRepository = productImageRepository;
            _projectCollectionProductRepository = projectCollectionProductRepository;
            _orderItemArtworkRepository = orderItemArtworkRepository;
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
    }
}
