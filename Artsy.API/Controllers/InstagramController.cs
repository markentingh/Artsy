using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Artsy.API.Models;
using Artsy.API.Models.Projects;
using Artsy.API.Services;
using Artsy.Auth.Services;
using Artsy.Data.Entities.Auth;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Auth;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.API.Controllers
{
    [Route("/api/instagram")]
    public class InstagramController : ApiController
    {
        readonly IAppUserRepository _userRepository;
        readonly IAppUserInstagramAccountRepository _instagramAccountRepository;
        readonly IHttpClientFactory _httpClientFactory;
        readonly IAuthService _authService;
        readonly IProjectRepository _projectRepository;
        readonly IProjectCollectionArtworkRepository _artworkRepository;
        readonly IProjectCollectionProductImageRepository _productImageRepository;
        readonly IProjectCollectionInstagramPostRepository _instagramPostRepository;
        readonly IProjectCollectionInstagramPostImageRepository _instagramPostImageRepository;
        readonly IImageService _imageService;

        public InstagramController(
            IAppUserRepository userRepository,
            IAppUserInstagramAccountRepository instagramAccountRepository,
            IHttpClientFactory httpClientFactory,
            IAuthService authService,
            IProjectRepository projectRepository,
            IProjectCollectionArtworkRepository artworkRepository,
            IProjectCollectionProductImageRepository productImageRepository,
            IProjectCollectionInstagramPostRepository instagramPostRepository,
            IProjectCollectionInstagramPostImageRepository instagramPostImageRepository,
            IImageService imageService)
        {
            _userRepository = userRepository;
            _instagramAccountRepository = instagramAccountRepository;
            _httpClientFactory = httpClientFactory;
            _authService = authService;
            _projectRepository = projectRepository;
            _artworkRepository = artworkRepository;
            _productImageRepository = productImageRepository;
            _instagramPostRepository = instagramPostRepository;
            _instagramPostImageRepository = instagramPostImageRepository;
            _imageService = imageService;
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

        [HttpPost("post-to-social-media")]
        [Authorize]
        public async Task<IActionResult> PostToSocialMedia([FromBody] PostToSocialMediaRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.ProjectId == Guid.Empty || request.CollectionId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Project ID and Collection ID are required." });

            if (request.Images == null || request.Images.Count == 0)
                return Json(new ApiResponse { success = false, message = "At least one image is required." });

            try
            {
                var project = await _projectRepository.GetByIdAsync(request.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Project not found." });

                if (!project.PostToInstagram || project.InstagramId == null || project.InstagramId == Guid.Empty)
                    return Json(new ApiResponse { success = false, message = "Instagram posting is not enabled for this project." });

                var igAccount = await _instagramAccountRepository.GetByIdAsync(project.InstagramId.Value);
                if (igAccount == null || igAccount.AppUserId != userId)
                    return Json(new ApiResponse { success = false, message = "Instagram account not found." });

                if (string.IsNullOrEmpty(igAccount.MetaAccessToken))
                    return Json(new ApiResponse { success = false, message = "Instagram account is not properly connected." });

                var sortedImages = request.Images.OrderBy(i => i.SortOrder).ToList();
                var mediaContainerIds = new List<string>();

                foreach (var img in sortedImages)
                {
                    byte[]? imgBytes = null;
                    if (img.ProductImageId.HasValue)
                    {
                        var productImage = await _productImageRepository.GetByIdAsync(img.ProductImageId.Value);
                        if (productImage == null || !productImage.Accepted || !productImage.Active) continue;
                        imgBytes = await _imageService.GetProjectCollectionProductImageAsync(project.Id, request.CollectionId, productImage.Id);
                    }
                    else if (img.ArtworkId.HasValue && img.ItemId.HasValue)
                    {
                        var artwork = await _artworkRepository.GetByIdAsync(img.ArtworkId.Value);
                        if (artwork == null || !artwork.Accepted || !artwork.Active) continue;
                        imgBytes = await _imageService.GetProjectCollectionArtworkImageAsync(project.Id, request.CollectionId, img.ItemId.Value, artwork.Id);
                    }

                    if (imgBytes == null || imgBytes.Length == 0) continue;

                    var base64 = Convert.ToBase64String(imgBytes);
                    var mediaContainerId = await CreateInstagramMediaContainer(igAccount, base64, request.Description);
                    if (!string.IsNullOrEmpty(mediaContainerId))
                        mediaContainerIds.Add(mediaContainerId);
                }

                if (mediaContainerIds.Count == 0)
                    return Json(new ApiResponse { success = false, message = "Failed to create any media containers for the post." });

                var post = await _instagramPostRepository.CreateAsync(new ProjectCollectionInstagramPost
                {
                    ProjectId = request.ProjectId,
                    CollectionId = request.CollectionId,
                    InstagramAccountId = igAccount.Id,
                    Description = request.Description,
                    ContainerId = mediaContainerIds.First(),
                    Status = 1,
                });

                for (var i = 0; i < sortedImages.Count; i++)
                {
                    var img = sortedImages[i];
                    if (img.ProductImageId.HasValue || img.ArtworkId.HasValue)
                    {
                        await _instagramPostImageRepository.CreateAsync(new ProjectCollectionInstagramPostImage
                        {
                            InstagramPostId = post.Id,
                            ProductImageId = img.ProductImageId,
                            ArtworkId = img.ArtworkId,
                            SortOrder = i,
                        });
                    }
                }

                var publishResult = await PublishInstagramMedia(igAccount, mediaContainerIds);
                if (!publishResult)
                    return Json(new ApiResponse { success = false, message = "Failed to publish media to Instagram." });

                return Json(new ApiResponse { success = true, data = new { postId = post.Id, mediaCount = mediaContainerIds.Count } });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        private async Task<string?> CreateInstagramMediaContainer(AppUserInstagramAccount account, string base64Image, string caption)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var imageUrl = $"data:image/jpeg;base64,{base64Image}";

                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "image_url", imageUrl },
                    { "caption", caption },
                    { "access_token", account.MetaAccessToken }
                });

                var response = await client.PostAsync($"https://graph.instagram.com/{account.InstagramBusinessAccountId}/media", content);
                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("id", out var idProp))
                    return idProp.GetString();

                return null;
            }
            catch
            {
                return null;
            }
        }

        private async Task<bool> PublishInstagramMedia(AppUserInstagramAccount account, List<string> mediaContainerIds)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var creationId = mediaContainerIds.First();

                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "creation_id", creationId },
                    { "access_token", account.MetaAccessToken }
                });

                var response = await client.PostAsync($"https://graph.instagram.com/{account.InstagramBusinessAccountId}/media_publish", content);
                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);

                return doc.RootElement.TryGetProperty("id", out _);
            }
            catch
            {
                return false;
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
