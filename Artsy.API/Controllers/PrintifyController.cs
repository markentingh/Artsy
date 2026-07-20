using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Artsy.API.Models;
using Artsy.API.Models.Printify;
using Artsy.API.Services;
using Artsy.Auth.Services;
using Artsy.Data.Entities;
using Artsy.Data.Entities.Auth;
using Artsy.Data.Interfaces;
using Artsy.Data.Interfaces.Auth;

namespace Artsy.API.Controllers
{
    [Route("/api/printify")]
    public class PrintifyController : ApiController
    {
        readonly IAppUserRepository _userRepository;
        readonly IHttpClientFactory _httpClientFactory;
        readonly IAuthService _authService;
        readonly IPrintifyBlueprintRepository _printifyBlueprintRepo;
        readonly IPrintifyBlueprintPrintProviderRepository _printProviderRepo;
        readonly IPrintifyBlueprintVariantRepository _variantRepo;
        readonly IPrintifyBlueprintVariantPlaceholderRepository _placeholderRepo;
        readonly IImageService _imageService;

        public PrintifyController(
            IAppUserRepository userRepository,
            IHttpClientFactory httpClientFactory,
            IAuthService authService,
            IPrintifyBlueprintRepository printifyBlueprintRepo,
            IPrintifyBlueprintPrintProviderRepository printProviderRepo,
            IPrintifyBlueprintVariantRepository variantRepo,
            IPrintifyBlueprintVariantPlaceholderRepository placeholderRepo,
            IImageService imageService)
        {
            _userRepository = userRepository;
            _httpClientFactory = httpClientFactory;
            _authService = authService;
            _printifyBlueprintRepo = printifyBlueprintRepo;
            _printProviderRepo = printProviderRepo;
            _variantRepo = variantRepo;
            _placeholderRepo = placeholderRepo;
            _imageService = imageService;
        }

        private string RedirectUri => $"{_authService.Settings().Domain.TrimEnd('/')}/api/printify/callback";

        private HttpClient CreatePrintifyClient(string? accessToken = null)
        {
            var handler = new SocketsHttpHandler
            {
                ConnectCallback = async (context, cancellationToken) =>
                {
                    var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    await socket.ConnectAsync(context.DnsEndPoint, cancellationToken);
                    return new NetworkStream(socket, ownsSocket: true);
                }
            };
            var client = new HttpClient(handler);
            var token = accessToken ?? ConnectionSettings.PrintifyApiToken;
            if (!string.IsNullOrEmpty(token))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        [HttpGet("status")]
        [Authorize]
        public async Task<IActionResult> Status()
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            try
            {
                var user = await _userRepository.FindByGuidAsync(userId);
                if (user == null)
                    return Json(new ApiResponse { success = false, message = "User not found" });

                var hasApiToken = !string.IsNullOrEmpty(ConnectionSettings.PrintifyApiToken);
                var shops = hasApiToken ? await GetAccountInfo() : [];
                var connected = hasApiToken || !string.IsNullOrEmpty(user.PrintifyAccessToken);

                return Json(new ApiResponse
                {
                    success = true,
                    data = new
                    {
                        connected,
                        shops,
                        viaApiToken = hasApiToken
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("connect")]
        [Authorize]
        public async Task<IActionResult> Connect()
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (!string.IsNullOrEmpty(ConnectionSettings.PrintifyApiToken))
            {
                var shops = await GetAccountInfo();
                return Json(new ApiResponse { success = true, data = new { connected = true, shops, viaApiToken = true } });
            }

            if (string.IsNullOrEmpty(ConnectionSettings.PrintifyClientId) || string.IsNullOrEmpty(ConnectionSettings.PrintifySecretKey) || string.IsNullOrEmpty(RedirectUri))
                return Json(new ApiResponse { success = false, message = "Printify OAuth is not configured." });

            try
            {
                var state = OAuthHelper.GenerateState();
                var user = await _userRepository.FindByGuidAsync(userId);
                if (user != null)
                {
                    user.OAuthState = state;
                    _userRepository.UpdateOAuthState(user);
                }

                var url = $"https://www.printify.com/oauth/authorize?" +
                          $"client_id={Uri.EscapeDataString(ConnectionSettings.PrintifyClientId)}" +
                          $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
                          $"&response_type=code" +
                          $"&scope={Uri.EscapeDataString("shops:read shops:manage catalog:read orders:read orders:write products:read products:write webhooks:read webhooks:write uploads:read uploads:write print_providers:read user:info")}" +
                          $"&state={Uri.EscapeDataString(state)}";

                return Json(new ApiResponse { success = true, data = new { url } });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("callback")]
        [AllowAnonymous]
        public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state)
        {
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
                return Json(new ApiResponse { success = false, message = "Invalid callback parameters." });

            try
            {
                var user = await _userRepository.FindByOAuthStateAsync(state);
                if (user == null)
                    return Json(new ApiResponse { success = false, message = "Invalid or expired state." });

                var tokenResponse = await ExchangeCodeForToken(code);
                if (string.IsNullOrEmpty(tokenResponse.access_token))
                    return Json(new ApiResponse { success = false, message = "Failed to obtain access token." });

                var shops = await GetAccountInfo(tokenResponse.access_token);

                user.PrintifyAccessToken = tokenResponse.access_token;
                user.PrintifyRefreshToken = tokenResponse.refresh_token;
                user.PrintifyTokensExpireAtUtc = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in > 0 ? tokenResponse.expires_in : 3600);
                user.PrintifyShopId = string.Join(", ", shops.Select(s => s.Id));
                user.OAuthState = null;
                _userRepository.UpdatePrintifyTokens(user);

                return Json(new ApiResponse { success = true, data = new { shops } });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        private async Task<(string access_token, string refresh_token, int expires_in)> ExchangeCodeForToken(string code)
        {
            var client = _httpClientFactory.CreateClient();
            var content = new StringContent(JsonSerializer.Serialize(new
            {
                grant_type = "authorization_code",
                client_id = ConnectionSettings.PrintifyClientId,
                client_secret = ConnectionSettings.PrintifySecretKey,
                code,
                redirect_uri = RedirectUri
            }), Encoding.UTF8, "application/json");

            var response = await client.PostAsync("https://api.printify.com/v1/oauth/token", content);
            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            return (
                doc.RootElement.GetProperty("access_token").GetString() ?? "",
                doc.RootElement.TryGetProperty("refresh_token", out var refresh) ? refresh.GetString() ?? "" : "",
                doc.RootElement.TryGetProperty("expires_in", out var expires) ? expires.GetInt32() : 3600
            );
        }

        private async Task<List<PrintifyShop>> GetAccountInfo(string? accessToken = null)
        {
            using var client = CreatePrintifyClient(accessToken);
            var response = await client.GetAsync("https://api.printify.com/v1/shops.json");
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<PrintifyShop>>(json) ?? [];
        }

        [HttpGet("blueprints")]
        [Authorize]
        public async Task<IActionResult> GetBlueprints([FromQuery] string? keyword, [FromQuery] string? brand, [FromQuery] int start = 0, [FromQuery] int length = 20)
        {
            try
            {
                var kw = keyword ?? "";
                var br = brand ?? "all";
                var results = await _printifyBlueprintRepo.SearchAsync(kw, br, start, length);
                var total = await _printifyBlueprintRepo.GetCountAsync(kw, br);

                var blueprints = results.Select(bp => new
                {
                    id = bp.BlueprintId,
                    title = bp.Title,
                    brand = bp.Brand,
                    model = bp.Model,
                    description = bp.Description,
                    imageCount = bp.ImageCount
                }).ToList();

                return Json(new ApiResponse { success = true, data = new { blueprints, total, hasMore = (start + length) < total } });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("brands")]
        [Authorize]
        public async Task<IActionResult> GetBrands()
        {
            try
            {
                var brands = await _printifyBlueprintRepo.GetBrandsAsync();
                return Json(new ApiResponse { success = true, data = new { brands } });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("blueprints/{blueprintId}")]
        [Authorize]
        public async Task<IActionResult> GetBlueprintDetail(int blueprintId)
        {
            try
            {
                var cached = await _printifyBlueprintRepo.GetByBlueprintIdAsync(blueprintId);
                if (cached == null || !cached.Published)
                    return Json(new ApiResponse { success = false, message = "Blueprint not found in catalog" });

                var providers = await _printProviderRepo.GetByBlueprintIdAsync(blueprintId);
                var printProviders = providers.Select(pp => new
                {
                    id = pp.PrintProviderId,
                    title = pp.Title,
                    decoration_methods = JsonSerializer.Deserialize<string[]>(pp.DecorationMethods) ?? Array.Empty<string>()
                }).ToList();

                return Json(new ApiResponse
                {
                    success = true,
                    data = new
                    {
                        blueprint = new
                        {
                            id = cached.BlueprintId,
                            title = cached.Title,
                            brand = cached.Brand,
                            model = cached.Model,
                            description = cached.Description,
                            imageCount = cached.ImageCount
                        },
                        printProviders
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("blueprints/{blueprintId}/print-providers/{printProviderId}/variants")]
        [Authorize]
        public async Task<IActionResult> GetBlueprintVariants(int blueprintId, int printProviderId)
        {
            try
            {
                var cachedVariants = await _variantRepo.GetByBlueprintAndProviderAsync(blueprintId, printProviderId);

                var variantIds = cachedVariants.Select(v => v.VariantId).ToList();
                var allPlaceholders = new Dictionary<int, List<object>>();
                if (variantIds.Count > 0)
                {
                    foreach (var vid in variantIds)
                    {
                        var phs = await _placeholderRepo.GetByVariantIdAsync(vid);
                        allPlaceholders[vid] = phs.Select(ph => (object)new
                        {
                            position = ph.Position,
                            decoration_method = ph.DecorationMethod,
                            height = ph.Height,
                            width = ph.Width
                        }).ToList();
                    }
                }

                var variants = cachedVariants.Select(v => new
                {
                    id = v.VariantId,
                    title = v.Title,
                    options = JsonSerializer.Deserialize<Dictionary<string, string>>(v.Options) ?? new Dictionary<string, string>(),
                    placeholders = allPlaceholders.TryGetValue(v.VariantId, out var phs) ? phs : new List<object>(),
                    decoration_methods = JsonSerializer.Deserialize<string[]>(v.DecorationMethods) ?? Array.Empty<string>()
                }).ToList();

                return Json(new ApiResponse { success = true, data = new { variants } });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("blueprints/{blueprintId}/print-providers/{printProviderId}/variant-availability")]
        [Authorize]
        public async Task<IActionResult> GetVariantAvailability(int blueprintId, int printProviderId)
        {
            try
            {
                using var client = CreatePrintifyClient();
                var response = await client.GetAsync($"https://api.printify.com/v1/catalog/blueprints/{blueprintId}/print_providers/{printProviderId}/variants.json?show-out-of-stock=0");
                if (!response.IsSuccessStatusCode)
                    return Json(new ApiResponse { success = false, message = $"Printify API error: {response.StatusCode}" });

                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonSerializer.Deserialize<JsonElement>(json);

                var inStockIds = new List<int>();
                if (doc.TryGetProperty("variants", out var vArr))
                {
                    foreach (var v in vArr.EnumerateArray())
                    {
                        var variantId = v.TryGetProperty("id", out var vid) ? vid.GetInt32() : 0;
                        inStockIds.Add(variantId);
                    }
                }

                return Json(new ApiResponse { success = true, data = new { inStockVariantIds = inStockIds } });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("blueprint-image")]
        [Authorize]
        public async Task<IActionResult> GetBlueprintImage([FromQuery] int blueprintId, [FromQuery] int index = 0, [FromQuery] bool thumb = false)
        {
            try
            {
                var bytes = await _imageService.GetPrintifyCatalogImageAsync(blueprintId, index, thumb);
                if (bytes.Length == 0)
                    return NotFound();
                return File(bytes, "image/jpeg");
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }
    }
}
