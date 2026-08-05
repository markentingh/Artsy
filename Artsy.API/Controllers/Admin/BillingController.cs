using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Artsy.API.Models;
using Artsy.API.Models.Billing;
using Artsy.Data.Entities;
using Artsy.Data.Interfaces;
using Artsy.Data.Interfaces.Auth;
using Artsy.Auth.Services;
using Artsy.Auth.Policies;

namespace Artsy.API.Controllers.Admin
{
    [Route("/api/admin/billing")]
    [Authorize(Policy = nameof(AuthConstants.Policy.ManageUsers))]
    public class BillingController : ApiController
    {
        readonly IProductRepository _productRepository;
        readonly ISubscriptionRepository _subscriptionRepository;
        readonly IInvoiceRepository _invoiceRepository;
        readonly IAppUserSubscriptionRepository _appUserSubscriptionRepository;
        readonly IAppUserAITokenRepository _appUserAITokenRepository;
        readonly IAppUserRepository _appUserRepository;

        public BillingController(
            IProductRepository productRepository,
            ISubscriptionRepository subscriptionRepository,
            IInvoiceRepository invoiceRepository,
            IAppUserSubscriptionRepository appUserSubscriptionRepository,
            IAppUserAITokenRepository appUserAITokenRepository,
            IAppUserRepository appUserRepository)
        {
            _productRepository = productRepository;
            _subscriptionRepository = subscriptionRepository;
            _invoiceRepository = invoiceRepository;
            _appUserSubscriptionRepository = appUserSubscriptionRepository;
            _appUserAITokenRepository = appUserAITokenRepository;
            _appUserRepository = appUserRepository;
        }

        #region Products
        [HttpGet("products")]
        public async Task<IActionResult> GetProducts()
        {
            try
            {
                var products = await _productRepository.GetAllAsync();
                return Json(new ApiResponse { success = true, data = products });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("products/save")]
        public async Task<IActionResult> SaveProduct([FromBody] SaveProductRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Title))
                    return Json(new ApiResponse { success = false, message = "Title is required." });

                if (request.Id > 0)
                {
                    var existing = await _productRepository.GetByIdAsync(request.Id);
                    if (existing == null)
                        return Json(new ApiResponse { success = false, message = "Product not found." });

                    existing.Title = request.Title;
                    existing.Price = request.Price;
                    existing.Tokens = request.Tokens;
                    await _productRepository.UpdateAsync(existing);
                    return Json(new ApiResponse { success = true, data = existing });
                }
                else
                {
                    var product = await _productRepository.CreateAsync(new Product
                    {
                        Title = request.Title,
                        Price = request.Price,
                        Tokens = request.Tokens
                    });
                    return Json(new ApiResponse { success = true, data = product });
                }
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("products/archive")]
        public async Task<IActionResult> ArchiveProduct([FromBody] ArchiveRequest request)
        {
            try
            {
                await _productRepository.ArchiveAsync(request.Id);
                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }
        #endregion

        #region Subscriptions
        [HttpGet("subscriptions")]
        public async Task<IActionResult> GetSubscriptions()
        {
            try
            {
                var subscriptions = await _subscriptionRepository.GetAllAsync();
                return Json(new ApiResponse { success = true, data = subscriptions });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("subscriptions/save")]
        public async Task<IActionResult> SaveSubscription([FromBody] SaveSubscriptionRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Title))
                    return Json(new ApiResponse { success = false, message = "Title is required." });

                if (request.Id > 0)
                {
                    var existing = await _subscriptionRepository.GetByIdAsync(request.Id);
                    if (existing == null)
                        return Json(new ApiResponse { success = false, message = "Subscription not found." });

                    existing.Title = request.Title;
                    existing.MonthlyProductId = request.MonthlyProductId;
                    existing.YearlyProductId = request.YearlyProductId;
                    existing.FeaturesJson = request.FeaturesJson;
                    await _subscriptionRepository.UpdateAsync(existing);
                    return Json(new ApiResponse { success = true, data = existing });
                }
                else
                {
                    var subscription = await _subscriptionRepository.CreateAsync(new Subscription
                    {
                        Title = request.Title,
                        MonthlyProductId = request.MonthlyProductId,
                        YearlyProductId = request.YearlyProductId,
                        FeaturesJson = request.FeaturesJson
                    });
                    return Json(new ApiResponse { success = true, data = subscription });
                }
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("subscriptions/archive")]
        public async Task<IActionResult> ArchiveSubscription([FromBody] ArchiveRequest request)
        {
            try
            {
                await _subscriptionRepository.ArchiveAsync(request.Id);
                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("subscriptions/reorder")]
        public async Task<IActionResult> ReorderSubscriptions([FromBody] ReorderSubscriptionsRequest request)
        {
            try
            {
                await _subscriptionRepository.ReorderAsync(request.Ids);
                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("subscriptions/set-featured")]
        public async Task<IActionResult> SetFeaturedSubscription([FromBody] ArchiveRequest request)
        {
            try
            {
                await _subscriptionRepository.SetFeaturedAsync(request.Id);
                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }
        #endregion

        #region AppUserSubscriptions
        [HttpGet("user-subscriptions")]
        public async Task<IActionResult> GetUserSubscriptions()
        {
            try
            {
                var subscriptions = await _appUserSubscriptionRepository.GetAllAsync();
                var users = await _appUserRepository.GetAll();
                var userLookup = users.ToDictionary(u => u.Id);
                var result = subscriptions.Select(s => new
                {
                    s.Id,
                    s.AppUserId,
                    email = userLookup.TryGetValue(s.AppUserId, out var user) ? user.Email : "",
                    s.SubscriptionId,
                    s.StartDate,
                    s.EndDate,
                    s.Cancelled,
                    s.DateCreated
                });
                return Json(new ApiResponse { success = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("user-subscriptions/cancel")]
        public async Task<IActionResult> CancelUserSubscription([FromBody] CancelUserSubscriptionRequest request)
        {
            try
            {
                await _appUserSubscriptionRepository.CancelAsync(request.Id);
                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }
        #endregion

        #region Invoices
        [HttpGet("invoices")]
        public async Task<IActionResult> GetInvoices()
        {
            try
            {
                var invoices = await _invoiceRepository.GetAllAsync();
                return Json(new ApiResponse { success = true, data = invoices });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }
        #endregion
    }
}
