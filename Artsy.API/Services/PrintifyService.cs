using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Artsy.API.Models.Printify;
using Artsy.Data.Interfaces.Auth;

namespace Artsy.API.Services
{
    public interface IPrintifyService
    {
        Task<List<PrintifyShopResponse>> GetShopsAsync(Guid userId);
        Task<PrintifyUploadResponse?> UploadImageAsync(Guid userId, string fileName, string base64Content);
        Task<PrintifyUploadResponse?> GetUploadAsync(Guid userId, string imageId);
        Task<PrintifyProductResult?> CreateProductAsync(Guid userId, int shopId, PrintifyProductRequest product);
        Task<bool> PublishProductAsync(Guid userId, int shopId, string productId, PrintifyPublishRequest request);
        Task<bool> UnpublishProductAsync(Guid userId, int shopId, string productId);
        Task<PrintifyProductResponse?> UpdateProductAsync(Guid userId, int shopId, string productId, PrintifyProductRequest product);
        Task<PrintifyProductResponse?> GetProductAsync(Guid userId, int shopId, string productId);
        Task<bool> DeleteProductAsync(Guid userId, int shopId, string productId);
    }

    public class PrintifyService : IPrintifyService
    {
        readonly IHttpClientFactory _httpClientFactory;
        readonly IAppUserRepository _userRepository;
        const string BaseUrl = "https://api.printify.com/v1";

        public PrintifyService(IHttpClientFactory httpClientFactory, IAppUserRepository userRepository)
        {
            _httpClientFactory = httpClientFactory;
            _userRepository = userRepository;
        }

        public async Task<List<PrintifyShopResponse>> GetShopsAsync(Guid userId)
        {
            var token = await GetAccessTokenAsync(userId);
            if (string.IsNullOrEmpty(token))
                return new List<PrintifyShopResponse>();

            using var client = CreatePrintifyClient(token);
            var response = await client.GetAsync($"{BaseUrl}/shops.json");
            if (!response.IsSuccessStatusCode)
                return new List<PrintifyShopResponse>();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<PrintifyShopResponse>>(json) ?? new List<PrintifyShopResponse>();
        }

        public async Task<PrintifyUploadResponse?> UploadImageAsync(Guid userId, string fileName, string base64Content)
        {
            var token = await GetAccessTokenAsync(userId);
            if (string.IsNullOrEmpty(token))
                return null;

            using var client = CreatePrintifyClient(token);
            var payload = new
            {
                file_name = fileName,
                contents = base64Content
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{BaseUrl}/uploads/images.json", content);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<PrintifyUploadResponse>(json);
        }

        public async Task<PrintifyUploadResponse?> GetUploadAsync(Guid userId, string imageId)
        {
            var token = await GetAccessTokenAsync(userId);
            if (string.IsNullOrEmpty(token))
                return null;

            using var client = CreatePrintifyClient(token);
            var response = await client.GetAsync($"{BaseUrl}/uploads/{imageId}.json");
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<PrintifyUploadResponse>(json);
        }

        public async Task<PrintifyProductResult?> CreateProductAsync(Guid userId, int shopId, PrintifyProductRequest product)
        {
            var token = await GetAccessTokenAsync(userId);
            if (string.IsNullOrEmpty(token))
                return null;

            using var client = CreatePrintifyClient(token);
            var content = new StringContent(JsonSerializer.Serialize(product), Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{BaseUrl}/shops/{shopId}/products.json", content);

            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                try
                {
                    var error = JsonSerializer.Deserialize<PrintifyError>(json);
                    var msg = !string.IsNullOrWhiteSpace(error?.Errors?.Reason)
                        ? error.Errors.Reason
                        : !string.IsNullOrWhiteSpace(error?.Error)
                            ? error.Error
                            : !string.IsNullOrWhiteSpace(error?.Message)
                                ? error.Message
                                : $"Printify API returned status {response.StatusCode}";
                    return new PrintifyProductResult { Error = msg };
                }
                catch
                {
                    return new PrintifyProductResult { Error = $"Printify API returned status {response.StatusCode}" };
                }
            }

            var productResp = JsonSerializer.Deserialize<PrintifyProductResponse>(json);
            return new PrintifyProductResult { Product = productResp };
        }

        public async Task<bool> PublishProductAsync(Guid userId, int shopId, string productId, PrintifyPublishRequest request)
        {
            var token = await GetAccessTokenAsync(userId);
            if (string.IsNullOrEmpty(token))
                return false;

            using var client = CreatePrintifyClient(token);
            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{BaseUrl}/shops/{shopId}/products/{productId}/publish.json", content);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> UnpublishProductAsync(Guid userId, int shopId, string productId)
        {
            var token = await GetAccessTokenAsync(userId);
            if (string.IsNullOrEmpty(token))
                return false;

            using var client = CreatePrintifyClient(token);
            var response = await client.PostAsync($"{BaseUrl}/shops/{shopId}/products/{productId}/unpublish.json", null);
            return response.IsSuccessStatusCode;
        }

        public async Task<PrintifyProductResponse?> UpdateProductAsync(Guid userId, int shopId, string productId, PrintifyProductRequest product)
        {
            var token = await GetAccessTokenAsync(userId);
            if (string.IsNullOrEmpty(token))
                return null;

            using var client = CreatePrintifyClient(token);
            var content = new StringContent(JsonSerializer.Serialize(product), Encoding.UTF8, "application/json");
            var response = await client.PutAsync($"{BaseUrl}/shops/{shopId}/products/{productId}.json", content);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<PrintifyProductResponse>(json);
        }

        public async Task<bool> DeleteProductAsync(Guid userId, int shopId, string productId)
        {
            var token = await GetAccessTokenAsync(userId);
            if (string.IsNullOrEmpty(token))
                return false;

            using var client = CreatePrintifyClient(token);
            var response = await client.DeleteAsync($"{BaseUrl}/shops/{shopId}/products/{productId}.json");
            return response.IsSuccessStatusCode;
        }

        public async Task<PrintifyProductResponse?> GetProductAsync(Guid userId, int shopId, string productId)
        {
            var token = await GetAccessTokenAsync(userId);
            if (string.IsNullOrEmpty(token))
                return null;

            using var client = CreatePrintifyClient(token);
            var response = await client.GetAsync($"{BaseUrl}/shops/{shopId}/products/{productId}.json");
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<PrintifyProductResponse>(json);
        }

        private async Task<string?> GetAccessTokenAsync(Guid userId)
        {
            var user = await _userRepository.FindByGuidAsync(userId);
            var token = user?.PrintifyAccessToken;
            if (string.IsNullOrEmpty(token))
                token = ConnectionSettings.PrintifyApiToken;
            return token;
        }

        private HttpClient CreatePrintifyClient(string accessToken)
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
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            return client;
        }
    }
}
