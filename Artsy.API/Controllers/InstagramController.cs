using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Artsy.API.Models;
using Artsy.API.Services;
using Artsy.Auth.Services;
using Artsy.Data.Entities.Auth;
using Artsy.Data.Interfaces.Auth;

namespace Artsy.API.Controllers
{
    [Route("/api/instagram")]
    public class InstagramController : ApiController
    {
        readonly IAppUserRepository _userRepository;
        readonly IAppUserInstagramAccountRepository _instagramAccountRepository;
        readonly IHttpClientFactory _httpClientFactory;
        readonly IAuthService _authService;

        public InstagramController(
            IAppUserRepository userRepository,
            IAppUserInstagramAccountRepository instagramAccountRepository,
            IHttpClientFactory httpClientFactory,
            IAuthService authService)
        {
            _userRepository = userRepository;
            _instagramAccountRepository = instagramAccountRepository;
            _httpClientFactory = httpClientFactory;
            _authService = authService;
        }

        private string RedirectUri => ConnectionSettings.InstagramRedirectUri;

        [HttpGet("accounts")]
        [Authorize]
        public async Task<IActionResult> Accounts()
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            try
            {
                var accounts = await _instagramAccountRepository.GetByUserIdAsync(userId);
                var result = accounts.Select(a => new
                {
                    id = a.Id,
                    instagramBusinessAccountId = a.InstagramBusinessAccountId,
                    username = a.Username,
                    profilePictureUrl = a.ProfilePictureUrl,
                    connected = !string.IsNullOrEmpty(a.MetaAccessToken)
                });

                return Json(new ApiResponse { success = true, data = result });
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

            if (string.IsNullOrEmpty(ConnectionSettings.InstagramAppId) || string.IsNullOrEmpty(ConnectionSettings.InstagramAppSecret) || string.IsNullOrEmpty(RedirectUri))
                return Json(new ApiResponse { success = false, message = "Instagram OAuth is not configured." });

            try
            {
                var state = OAuthHelper.GenerateState();
                var user = await _userRepository.FindByGuidAsync(userId);
                if (user != null)
                {
                    user.OAuthState = state;
                    _userRepository.UpdateOAuthState(user);
                }

                return Json(new ApiResponse { success = true, data = new { appId = ConnectionSettings.InstagramAppId, redirectUri = RedirectUri, state } });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("exchange")]
        [Authorize]
        public async Task<IActionResult> Exchange([FromBody] JsonElement body)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (!body.TryGetProperty("code", out var codeEl) || !body.TryGetProperty("state", out var stateEl))
                return Json(new ApiResponse { success = false, message = "Code and state are required." });

            var code = codeEl.GetString() ?? "";
            var state = stateEl.GetString() ?? "";

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
                if (string.IsNullOrEmpty(account.instagramBusinessAccountId))
                    return Json(new ApiResponse { success = false, message = "No Instagram Business Account found. Make sure your Facebook Page is linked to an Instagram Business Account." });

                var existing = await _instagramAccountRepository.GetByInstagramBusinessAccountIdAsync(user.Id!.Value, account.instagramBusinessAccountId);
                if (existing != null)
                {
                    user.OAuthState = null;
                    _userRepository.UpdateOAuthState(user);
                    return Json(new ApiResponse { success = false, message = $"Instagram account @{account.username} is already connected." });
                }

                var igAccount = await _instagramAccountRepository.UpsertAsync(new AppUserInstagramAccount
                {
                    AppUserId = user.Id!.Value,
                    InstagramBusinessAccountId = account.instagramBusinessAccountId,
                    MetaUserId = account.userId,
                    MetaAccessToken = tokenResponse.access_token,
                    MetaTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in > 0 ? tokenResponse.expires_in : 3600),
                    Username = account.username,
                    ProfilePictureUrl = account.profilePictureUrl
                });

                user.OAuthState = null;
                _userRepository.UpdateOAuthState(user);

                return Json(new ApiResponse { success = true, data = new { id = igAccount.Id, instagramBusinessAccountId = igAccount.InstagramBusinessAccountId, username = igAccount.Username } });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        private async Task<(string access_token, string refresh_token, int expires_in)> ExchangeCodeForToken(string code)
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"https://api.instagram.com/oauth/access_token";
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "client_id", ConnectionSettings.InstagramAppId },
                { "client_secret", ConnectionSettings.InstagramAppSecret },
                { "grant_type", "authorization_code" },
                { "redirect_uri", RedirectUri },
                { "code", code }
            });

            var response = await client.PostAsync(url, content);
            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("error", out var errorProp))
            {
                var errorMsg = errorProp.TryGetProperty("message", out var msgProp) ? msgProp.GetString() ?? "Unknown error" : "Unknown error";
                throw new Exception($"Instagram token exchange failed: {errorMsg}");
            }
            if (doc.RootElement.TryGetProperty("error_type", out var errorTypeProp))
            {
                var errorMsg = doc.RootElement.TryGetProperty("error_message", out var msgProp) ? msgProp.GetString() ?? "Unknown error" : "Unknown error";
                throw new Exception($"Instagram token exchange failed: {errorTypeProp.GetString()} - {errorMsg}");
            }

            return (
                doc.RootElement.TryGetProperty("access_token", out var tokenProp) ? tokenProp.GetString() ?? "" : "",
                doc.RootElement.TryGetProperty("refresh_token", out var refresh) ? refresh.GetString() ?? "" : "",
                doc.RootElement.TryGetProperty("expires_in", out var expires) ? expires.GetInt32() : 3600
            );
        }

        [HttpPost("disconnect")]
        [Authorize]
        public async Task<IActionResult> Disconnect([FromBody] JsonElement body)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (!body.TryGetProperty("id", out var idEl) || !Guid.TryParse(idEl.GetString(), out var accountId))
                return Json(new ApiResponse { success = false, message = "Account ID is required." });

            try
            {
                await _instagramAccountRepository.DeleteAsync(accountId, userId);
                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        private async Task<(string userId, string instagramBusinessAccountId, string username, string profilePictureUrl)> GetAccountInfo(string accessToken)
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"https://graph.instagram.com/me?fields=id,username,profile_picture_url&access_token={Uri.EscapeDataString(accessToken)}";
            var response = await client.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            var instagramBusinessAccountId = doc.RootElement.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
            var username = doc.RootElement.TryGetProperty("username", out var usernameProp) ? usernameProp.GetString() ?? "" : "";
            var profilePictureUrl = doc.RootElement.TryGetProperty("profile_picture_url", out var picProp) ? picProp.GetString() ?? "" : "";

            return (instagramBusinessAccountId, instagramBusinessAccountId, username, profilePictureUrl);
        }
    }
}
