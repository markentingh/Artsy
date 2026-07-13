using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text.Json;
using Artsy.API.Models;
using Artsy.API.Services;
using Artsy.Auth.Services;
using Artsy.Data.Entities.Auth;
using Artsy.Data.Interfaces.Auth;

namespace Artsy.API.Controllers
{
    [Route("/api/meta")]
    public class MetaController : ApiController
    {
        readonly IAppUserRepository _userRepository;
        readonly IHttpClientFactory _httpClientFactory;
        readonly IAuthService _authService;

        public MetaController(IAppUserRepository userRepository, IHttpClientFactory httpClientFactory, IAuthService authService)
        {
            _userRepository = userRepository;
            _httpClientFactory = httpClientFactory;
            _authService = authService;
        }

        private string RedirectUri => $"{_authService.Settings().Domain.TrimEnd('/')}/api/meta/callback";

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

                return Json(new ApiResponse
                {
                    success = true,
                    data = new
                    {
                        connected = !string.IsNullOrEmpty(user.MetaAccessToken),
                        userId = user.MetaUserId,
                        instagramBusinessAccountId = user.InstagramBusinessAccountId
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

            if (string.IsNullOrEmpty(ConnectionSettings.MetaAppId) || string.IsNullOrEmpty(ConnectionSettings.MetaAppSecret) || string.IsNullOrEmpty(RedirectUri))
                return Json(new ApiResponse { success = false, message = "Meta OAuth is not configured." });

            try
            {
                var state = OAuthHelper.GenerateState();
                var user = await _userRepository.FindByGuidAsync(userId);
                if (user != null)
                {
                    user.OAuthState = state;
                    _userRepository.UpdateOAuthState(user);
                }

                var url = $"https://www.facebook.com/v18.0/dialog/oauth?" +
                          $"client_id={Uri.EscapeDataString(ConnectionSettings.MetaAppId)}" +
                          $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
                          $"&scope={Uri.EscapeDataString("email,public_profile")}" +
                          $"&state={Uri.EscapeDataString(state)}" +
                          $"&response_type=code";

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

                var account = await GetAccountInfo(tokenResponse.access_token);

                user.MetaAccessToken = tokenResponse.access_token;
                user.MetaTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in > 0 ? tokenResponse.expires_in : 3600);
                user.MetaUserId = account.userId;
                user.InstagramBusinessAccountId = account.instagramBusinessAccountId;
                user.OAuthState = null;
                _userRepository.UpdateMetaTokens(user);

                return Json(new ApiResponse { success = true, data = new { userId = account.userId, instagramBusinessAccountId = account.instagramBusinessAccountId } });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        private async Task<(string access_token, string refresh_token, int expires_in)> ExchangeCodeForToken(string code)
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"https://graph.facebook.com/v18.0/oauth/access_token?" +
                      $"client_id={Uri.EscapeDataString(ConnectionSettings.MetaAppId)}" +
                      $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
                      $"&client_secret={Uri.EscapeDataString(ConnectionSettings.MetaAppSecret)}" +
                      $"&code={Uri.EscapeDataString(code)}";

            var response = await client.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            return (
                doc.RootElement.GetProperty("access_token").GetString() ?? "",
                doc.RootElement.TryGetProperty("refresh_token", out var refresh) ? refresh.GetString() ?? "" : "",
                doc.RootElement.TryGetProperty("expires_in", out var expires) ? expires.GetInt32() : 3600
            );
        }

        private async Task<(string userId, string instagramBusinessAccountId)> GetAccountInfo(string accessToken)
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"https://graph.facebook.com/me?fields=id,accounts{{instagram_business_account}}&access_token={Uri.EscapeDataString(accessToken)}";
            var response = await client.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var userId = doc.RootElement.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
            var instagramBusinessAccountId = "";
            if (doc.RootElement.TryGetProperty("accounts", out var accountsProp) &&
                accountsProp.TryGetProperty("data", out var dataProp) &&
                dataProp.ValueKind == JsonValueKind.Array &&
                dataProp.GetArrayLength() > 0)
            {
                var first = dataProp[0];
                if (first.TryGetProperty("instagram_business_account", out var igProp) &&
                    igProp.TryGetProperty("id", out var igIdProp))
                {
                    instagramBusinessAccountId = igIdProp.GetString() ?? "";
                }
            }
            return (userId, instagramBusinessAccountId);
        }
    }
}
