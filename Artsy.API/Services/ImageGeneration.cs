using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Artsy.API.Models.Projects;
using Microsoft.Extensions.Options;

namespace Artsy.API.Services
{
    public interface IImageGeneration
    {
        Task<byte[]> GenerateAsync(string imageModel, string imageModelJson, string? quality = null);
    }

    public class ImageGenerationOptions
    {
        public int TimeoutSeconds { get; set; } = 120;
        public Dictionary<string, ImageModelConfig> Models { get; set; } = new();
    }

    public class ImageModelConfig
    {
        public string ApiKey { get; set; } = "";
        public string Endpoint { get; set; } = "https://api.openai.com/v1/images/generations";
        public string Model { get; set; } = "gpt-image-2";
    }

    public class ImageGeneration : IImageGeneration
    {
        readonly IHttpClientFactory _httpClientFactory;
        readonly ImageGenerationOptions _options;

        public ImageGeneration(IHttpClientFactory httpClientFactory, IOptions<ImageGenerationOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
        }

        public async Task<byte[]> GenerateAsync(string imageModel, string imageModelJson, string? quality = null)
        {
            if (string.IsNullOrWhiteSpace(imageModel))
                throw new ArgumentException("Image model is required.", nameof(imageModel));

            if (string.IsNullOrWhiteSpace(imageModelJson))
                throw new ArgumentException("Image model JSON is required.", nameof(imageModelJson));

            var key = imageModel.ToLowerInvariant();
            switch (key)
            {
                case "openai":
                    return await GenerateOpenAIImageAsync(imageModelJson, quality);
                default:
                    throw new InvalidOperationException($"Image model '{imageModel}' is not supported.");
            }
        }

        async Task<byte[]> GenerateOpenAIImageAsync(string imageModelJson, string? quality = null)
        {
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var request = JsonSerializer.Deserialize<OpenAIImageRequest>(imageModelJson, jsonOptions);
            if (request == null)
                throw new ArgumentException("Image model JSON could not be deserialized.", nameof(imageModelJson));

            if (string.IsNullOrWhiteSpace(request.Prompt))
                throw new ArgumentException("Prompt is required.", nameof(request.Prompt));

            if (!_options.Models.TryGetValue("openai", out var config))
                throw new InvalidOperationException("OpenAI image model is not configured.");

            if (string.IsNullOrWhiteSpace(config.ApiKey))
                throw new InvalidOperationException("OpenAI API key is missing.");

            if (string.IsNullOrWhiteSpace(request.Model))
                request.Model = config.Model;

            if (request.N == null || request.N < 1)
                request.N = 1;

            if (string.IsNullOrWhiteSpace(request.Size))
                request.Size = "1024x1024";

            if (!string.IsNullOrWhiteSpace(quality))
                request.Quality = quality;
            else if (string.IsNullOrWhiteSpace(request.Quality))
                request.Quality = "medium";

            var jsonContent = JsonSerializer.Serialize(request, jsonOptions);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            using var client = _httpClientFactory.CreateClient("ImageGeneration");
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, config.Endpoint)
            {
                Content = content
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));
            var response = await client.SendAsync(httpRequest, cts.Token);
            var responseContent = await response.Content.ReadAsStringAsync(cts.Token);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Image generation failed: {response.StatusCode} - {responseContent}");

            var generationResponse = JsonSerializer.Deserialize<OpenAIImageResponse>(responseContent, jsonOptions);
            if (generationResponse?.Data == null || generationResponse.Data.Count == 0)
                throw new InvalidOperationException("No image returned from generation API.");

            var first = generationResponse.Data[0];

            if (!string.IsNullOrWhiteSpace(first.B64Json))
                return Convert.FromBase64String(first.B64Json);

            if (!string.IsNullOrWhiteSpace(first.Url))
            {
                using var imageResponse = await client.GetAsync(first.Url, cts.Token);
                if (!imageResponse.IsSuccessStatusCode)
                    throw new InvalidOperationException($"Failed to download generated image: {imageResponse.StatusCode}");

                return await imageResponse.Content.ReadAsByteArrayAsync(cts.Token);
            }

            throw new InvalidOperationException("Generated image did not contain a URL or base64 data.");
        }
    }
}
