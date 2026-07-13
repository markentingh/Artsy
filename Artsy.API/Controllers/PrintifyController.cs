using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Artsy.API.Models;
using Artsy.API.Models.Printify;
using Artsy.API.Services;
using Artsy.Auth.Services;
using Artsy.Data.Entities.Auth;
using Artsy.Data.Interfaces.Auth;

namespace Artsy.API.Controllers
{
    [Route("/api/printify")]
    public class PrintifyController : ApiController
    {
        readonly IAppUserRepository _userRepository;
        readonly IHttpClientFactory _httpClientFactory;
        readonly IAuthService _authService;

        public PrintifyController(IAppUserRepository userRepository, IHttpClientFactory httpClientFactory, IAuthService authService)
        {
            _userRepository = userRepository;
            _httpClientFactory = httpClientFactory;
            _authService = authService;
        }

        private string RedirectUri => $"{_authService.Settings().Domain.TrimEnd('/')}/api/printify/callback";

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
            var client = _httpClientFactory.CreateClient();
            var token = accessToken ?? ConnectionSettings.PrintifyApiToken;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await client.GetAsync("https://api.printify.com/v1/shops.json");
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<PrintifyShop>>(json) ?? [];
        }
    }
}
