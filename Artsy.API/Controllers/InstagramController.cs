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

        [HttpGet("collection-posted")]
        [Authorize]
        public async Task<IActionResult> CollectionPosted(Guid collectionId)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            try
            {
                var posts = await _instagramPostRepository.GetByCollectionIdAsync(collectionId);
                return Json(new ApiResponse { success = true, data = new { posted = posts.Any() } });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("collection-post")]
        [Authorize]
        public async Task<IActionResult> CollectionPost(Guid collectionId)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            try
            {
                var posts = await _instagramPostRepository.GetByCollectionIdAsync(collectionId);
                var post = posts.FirstOrDefault();
                if (post == null)
                    return Json(new ApiResponse { success = true, data = null });

                return Json(new ApiResponse { success = true, data = new
                {
                    id = post.Id,
                    description = post.Description,
                    permalink = post.Permalink,
                    created = post.Created
                }});
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
                    existing.MetaAccessToken = tokenResponse.access_token;
                    existing.MetaTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in > 0 ? tokenResponse.expires_in : 3600);
                    existing.Username = account.username;
                    existing.ProfilePictureUrl = account.profilePictureUrl;
                    await _instagramAccountRepository.UpsertAsync(existing);

                    user.OAuthState = null;
                    _userRepository.UpdateOAuthState(user);

                    return Json(new ApiResponse { success = true, data = new { id = existing.Id, instagramBusinessAccountId = existing.InstagramBusinessAccountId, username = existing.Username } });
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
                var errors = new List<string>();
                var domain = ConnectionSettings.MetaImagesDomain.TrimEnd('/');

                if (string.IsNullOrEmpty(domain))
                    return Json(new ApiResponse { success = false, message = "Meta Images Domain is not configured." });

                var isCarousel = sortedImages.Count > 1;

                foreach (var img in sortedImages)
                {
                    string? imageUrl = null;
                    if (img.ProductImageId.HasValue)
                    {
                        var productImage = await _productImageRepository.GetByIdAsync(img.ProductImageId.Value);
                        if (productImage == null || !productImage.Accepted || !productImage.Active) continue;
                        imageUrl = $"{domain}/meta/image/product/{img.ProductImageId.Value}";
                    }
                    else if (img.ArtworkId.HasValue && img.ItemId.HasValue)
                    {
                        var artwork = await _artworkRepository.GetByIdAsync(img.ArtworkId.Value);
                        if (artwork == null || !artwork.Accepted || !artwork.Active) continue;
                        imageUrl = $"{domain}/meta/image/artwork/{img.ArtworkId.Value}";
                    }

                    if (string.IsNullOrEmpty(imageUrl)) continue;

                    var (mediaContainerId, error) = await CreateInstagramMediaContainer(igAccount, imageUrl, request.Description, isCarousel);
                    if (!string.IsNullOrEmpty(mediaContainerId))
                        mediaContainerIds.Add(mediaContainerId);
                    else if (!string.IsNullOrEmpty(error))
                        errors.Add(error);
                }

                if (mediaContainerIds.Count == 0)
                {
                    var errorMsg = errors.Count > 0
                        ? $"Failed to create any media containers for the post. Errors: {string.Join("; ", errors)}"
                        : "Failed to create any media containers for the post.";
                    var tokenExpired = errors.Any(e => e.Contains("Session has expired") || e.Contains("access token"));
                    return Json(new ApiResponse { success = false, message = errorMsg, data = tokenExpired ? new { tokenExpired = true } : null });
                }

                string publishContainerId;
                if (isCarousel)
                {
                    foreach (var childId in mediaContainerIds)
                    {
                        var ready = await WaitForMediaReady(igAccount, childId);
                        if (!ready)
                            return Json(new ApiResponse { success = false, message = "One or more images failed to process on Instagram." });
                    }

                    var (carouselId, carouselError) = await CreateInstagramCarouselContainer(igAccount, mediaContainerIds, request.Description);
                    if (string.IsNullOrEmpty(carouselId))
                    {
                        return Json(new ApiResponse { success = false, message = $"Failed to create carousel container. {carouselError}" });
                    }

                    var carouselReady = await WaitForMediaReady(igAccount, carouselId);
                    if (!carouselReady)
                        return Json(new ApiResponse { success = false, message = "Carousel failed to process on Instagram." });

                    publishContainerId = carouselId;
                }
                else
                {
                    var ready = await WaitForMediaReady(igAccount, mediaContainerIds.First());
                    if (!ready)
                        return Json(new ApiResponse { success = false, message = "Image failed to process on Instagram." });
                    publishContainerId = mediaContainerIds.First();
                }

                var (publishSuccess, mediaId, publishError) = await PublishInstagramMedia(igAccount, publishContainerId);
                if (!publishSuccess)
                    return Json(new ApiResponse { success = false, message = $"Failed to publish media to Instagram. {publishError}" });

                string? permalink = null;
                if (!string.IsNullOrEmpty(mediaId))
                {
                    await Task.Delay(2000);
                    permalink = await GetMediaPermalink(igAccount, mediaId);
                }

                var post = await _instagramPostRepository.CreateAsync(new ProjectCollectionInstagramPost
                {
                    ProjectId = request.ProjectId,
                    CollectionId = request.CollectionId,
                    InstagramAccountId = igAccount.Id,
                    Description = request.Description,
                    ContainerId = publishContainerId,
                    Permalink = permalink,
                    Status = 1,
                });

                if (!string.IsNullOrEmpty(permalink))
                {
                    await _instagramPostRepository.UpdatePermalinkAsync(post.Id, permalink);
                }

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

                return Json(new ApiResponse { success = true, data = new { postId = post.Id, mediaCount = mediaContainerIds.Count, permalink } });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        private async Task<(string? containerId, string? error)> CreateInstagramMediaContainer(AppUserInstagramAccount account, string imageUrl, string caption, bool isCarouselItem)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();

                var fields = new Dictionary<string, string>
                {
                    { "image_url", imageUrl },
                    { "access_token", account.MetaAccessToken }
                };

                if (isCarouselItem)
                {
                    fields["is_carousel_item"] = "true";
                }
                else
                {
                    fields["caption"] = caption;
                }

                var content = new FormUrlEncodedContent(fields);

                var response = await client.PostAsync($"https://graph.instagram.com/{account.InstagramBusinessAccountId}/media", content);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return (null, $"Instagram API returned {response.StatusCode}: {json}");
                }

                var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("id", out var idProp))
                    return (idProp.GetString(), null);

                if (doc.RootElement.TryGetProperty("error", out var errorProp))
                {
                    var msg = errorProp.TryGetProperty("message", out var msgProp) ? msgProp.GetString() ?? json : json;
                    return (null, msg);
                }

                return (null, $"Unexpected response: {json}");
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        private async Task<(string? containerId, string? error)> CreateInstagramCarouselContainer(AppUserInstagramAccount account, List<string> childrenIds, string caption)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();

                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "media_type", "CAROUSEL" },
                    { "children", string.Join(",", childrenIds) },
                    { "caption", caption },
                    { "access_token", account.MetaAccessToken }
                });

                var response = await client.PostAsync($"https://graph.instagram.com/{account.InstagramBusinessAccountId}/media", content);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return (null, $"Instagram API returned {response.StatusCode}: {json}");
                }

                var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("id", out var idProp))
                    return (idProp.GetString(), null);

                if (doc.RootElement.TryGetProperty("error", out var errorProp))
                {
                    var msg = errorProp.TryGetProperty("message", out var msgProp) ? msgProp.GetString() ?? json : json;
                    return (null, msg);
                }

                return (null, $"Unexpected response: {json}");
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        private async Task<bool> WaitForMediaReady(AppUserInstagramAccount account, string containerId, int maxAttempts = 10, int delayMs = 3000)
        {
            var client = _httpClientFactory.CreateClient();
            for (var i = 0; i < maxAttempts; i++)
            {
                await Task.Delay(delayMs);
                var url = $"https://graph.instagram.com/{containerId}?fields=status_code&access_token={Uri.EscapeDataString(account.MetaAccessToken)}";
                var response = await client.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("status_code", out var statusProp))
                {
                    var status = statusProp.GetString();
                    if (status == "FINISHED") return true;
                    if (status == "ERROR") return false;
                }
            }
            return false;
        }

        private async Task<(bool success, string? mediaId, string? error)> PublishInstagramMedia(AppUserInstagramAccount account, string containerId)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();

                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "creation_id", containerId },
                    { "access_token", account.MetaAccessToken }
                });

                var response = await client.PostAsync($"https://graph.instagram.com/{account.InstagramBusinessAccountId}/media_publish", content);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return (false, null, $"Instagram API returned {response.StatusCode}: {json}");
                }

                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("id", out var idProp))
                    return (true, idProp.GetString(), null);

                return (false, null, $"Unexpected response: {json}");
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        private async Task<string?> GetMediaPermalink(AppUserInstagramAccount account, string mediaId)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var url = $"https://graph.instagram.com/{mediaId}?fields=permalink&access_token={Uri.EscapeDataString(account.MetaAccessToken)}";
                var response = await client.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("permalink", out var permalinkProp))
                    return permalinkProp.GetString();
            }
            catch
            {
            }
            return null;
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
