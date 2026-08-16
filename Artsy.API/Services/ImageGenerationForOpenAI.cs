using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Artsy.API.Models.Projects;
using Artsy.Data.Entities;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace Artsy.API.Services
{
    public class ImageGenerationOptions
    {
        public int TimeoutSeconds { get; set; } = 300;
        public Dictionary<string, ImageModelConfig> Models { get; set; } = new();
    }

    public class ImageModelConfig
    {
        public string ApiKey { get; set; } = "";
        public string Endpoint { get; set; } = "https://api.openai.com/v1/responses";
        public string ImageEndpoint { get; set; } = "https://api.openai.com/v1/images/generations";
        public string ImageEditEndpoint { get; set; } = "https://api.openai.com/v1/images/edits";
    }

    public class ImageGenerationForOpenAI : IImageGeneration
    {
        readonly IHttpClientFactory _httpClientFactory;
        readonly ImageGenerationOptions _options;
        readonly IImageService _imageService;

        public string ModelKey => "openai";

        public IImageTokens CreateTokenizer(ImageGenerationModel model)
        {
            return new ImageTokensForOpenAI(model.CPMITTokens, model.CPMIITokens, model.CPMOTokens);
        }

        public ImageGenerationForOpenAI(IHttpClientFactory httpClientFactory, IOptions<ImageGenerationOptions> options, IImageService imageService)
        {
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
            _imageService = imageService;
        }

        public async Task<ImageGenerationResult> GenerateAsync(ImageGenerationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Prompt))
                throw new ArgumentException("Prompt is required.", nameof(request));

            if (request.InputImages != null && request.InputImages.Count > 0)
            {
                var resized = new List<byte[]>(request.InputImages.Count);
                foreach (var img in request.InputImages)
                    resized.Add(await _imageService.ResizeImageMaxAsync(img, 1024));
                request.InputImages = resized;
            }

            if (request.UseResponsesApi)
                return await GenerateViaResponsesApiAsync(request);

            if (request.InputImages != null && request.InputImages.Count > 0)
                return await GenerateViaImageEditApiAsync(request);

            return await GenerateViaImageApiAsync(request);
        }

        async Task<ImageGenerationResult> GenerateViaImageApiAsync(ImageGenerationRequest request)
        {
            if (!_options.Models.TryGetValue("openai", out var config))
                throw new InvalidOperationException("OpenAI image model is not configured.");

            if (string.IsNullOrWhiteSpace(config.ApiKey))
                throw new InvalidOperationException("OpenAI API key is missing.");

            var model = string.IsNullOrWhiteSpace(request.Model) ? "gpt-image-2" : request.Model;
            var size = FindBestResolution($"{request.Width}x{request.Height}");
            var quality = string.IsNullOrWhiteSpace(request.Quality) ? "medium" : request.Quality;

            var images = new List<OpenAIImageReference>();
            if (request.InputImages != null && request.InputImages.Count > 0)
            {
                foreach (var img in request.InputImages)
                {
                    if (img != null && img.Length > 0)
                    {
                        images.Add(new OpenAIImageReference
                        {
                            ImageUrl = GetImageDataUrl(img)
                        });
                    }
                }
            }

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var imageApiRequest = new OpenAIImageRequest
            {
                Model = model,
                Prompt = request.Prompt,
                N = 1,
                Size = size,
                Quality = quality,
                Images = images.Count > 0 ? images : null
            };

            var jsonContent = JsonSerializer.Serialize(imageApiRequest, jsonOptions);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            using var client = _httpClientFactory.CreateClient("ImageGeneration");
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, config.ImageEndpoint)
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
            byte[]? imageBytes = null;

            if (!string.IsNullOrWhiteSpace(first.B64Json))
                imageBytes = Convert.FromBase64String(first.B64Json);
            else if (!string.IsNullOrWhiteSpace(first.Url))
            {
                using var imageResponse = await client.GetAsync(first.Url, cts.Token);
                if (!imageResponse.IsSuccessStatusCode)
                    throw new InvalidOperationException($"Failed to download generated image: {imageResponse.StatusCode}");
                imageBytes = await imageResponse.Content.ReadAsByteArrayAsync(cts.Token);
            }

            if (imageBytes == null || imageBytes.Length == 0)
                throw new InvalidOperationException("Generated image did not contain a URL or base64 data.");

            return new ImageGenerationResult { ImageBytes = imageBytes };
        }

        async Task<ImageGenerationResult> GenerateViaImageEditApiAsync(ImageGenerationRequest request)
        {
            if (!_options.Models.TryGetValue("openai", out var config))
                throw new InvalidOperationException("OpenAI image model is not configured.");

            if (string.IsNullOrWhiteSpace(config.ApiKey))
                throw new InvalidOperationException("OpenAI API key is missing.");

            var model = string.IsNullOrWhiteSpace(request.Model) ? "gpt-image-2" : request.Model;
            var size = FindBestResolution($"{request.Width}x{request.Height}");
            var quality = string.IsNullOrWhiteSpace(request.Quality) ? "medium" : request.Quality;

            var images = new List<OpenAIImageReference>();
            for (var i = 0; i < request.InputImages.Count; i++)
            {
                var img = request.InputImages[i];
                if (img != null && img.Length > 0)
                {
                    images.Add(new OpenAIImageReference
                    {
                        ImageUrl = GetImageDataUrl(img)
                    });
                }
            }

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var imageEditRequest = new OpenAIImageRequest
            {
                Model = model,
                Prompt = request.Prompt,
                N = 1,
                Size = size,
                Quality = quality,
                Images = images
            };

            var jsonContent = JsonSerializer.Serialize(imageEditRequest, jsonOptions);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            using var client = _httpClientFactory.CreateClient("ImageGeneration");
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, config.ImageEditEndpoint)
            {
                Content = content
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));
            var response = await client.SendAsync(httpRequest, cts.Token);
            var responseContent = await response.Content.ReadAsStringAsync(cts.Token);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Image edit failed: {response.StatusCode} - {responseContent}");

            var generationResponse = JsonSerializer.Deserialize<OpenAIImageResponse>(responseContent, jsonOptions);
            if (generationResponse?.Data == null || generationResponse.Data.Count == 0)
                throw new InvalidOperationException("No image returned from edit API.");

            var first = generationResponse.Data[0];
            byte[]? imageBytes = null;

            if (!string.IsNullOrWhiteSpace(first.B64Json))
                imageBytes = Convert.FromBase64String(first.B64Json);
            else if (!string.IsNullOrWhiteSpace(first.Url))
            {
                using var imageResponse = await client.GetAsync(first.Url, cts.Token);
                if (!imageResponse.IsSuccessStatusCode)
                    throw new InvalidOperationException($"Failed to download generated image: {imageResponse.StatusCode}");
                imageBytes = await imageResponse.Content.ReadAsByteArrayAsync(cts.Token);
            }

            if (imageBytes == null || imageBytes.Length == 0)
                throw new InvalidOperationException("Generated image did not contain a URL or base64 data.");

            return new ImageGenerationResult { ImageBytes = imageBytes };
        }

        async Task<ImageGenerationResult> GenerateViaResponsesApiAsync(ImageGenerationRequest request)
        {
            if (!_options.Models.TryGetValue("openai", out var config))
                throw new InvalidOperationException("OpenAI image model is not configured.");

            if (string.IsNullOrWhiteSpace(config.ApiKey))
                throw new InvalidOperationException("OpenAI API key is missing.");

            var imageModel = string.IsNullOrWhiteSpace(request.Model) ? "gpt-image-2" : request.Model;
            var toolSize = FindBestResolution($"{request.Width}x{request.Height}");
            var toolQuality = string.IsNullOrWhiteSpace(request.Quality) ? "medium" : request.Quality;

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var responsesRequest = new OpenAIResponsesRequest
            {
                Model = "gpt-4o",
                Tools = new List<OpenAITool>
                {
                    new()
                    {
                        Type = "image_generation",
                        Model = imageModel,
                        Size = toolSize,
                        Quality = toolQuality
                    }
                },
                ToolChoice = "auto"
            };

            if (!string.IsNullOrWhiteSpace(request.PreviousResponseId))
                responsesRequest.PreviousResponseId = request.PreviousResponseId;

            var contentItems = new List<OpenAIInputContent>
            {
                new() { Type = "input_text", Text = request.Prompt }
            };

            if (request.InputImages != null && request.InputImages.Count > 0)
            {
                foreach (var imgBytes in request.InputImages)
                {
                    if (imgBytes != null && imgBytes.Length > 0)
                    {
                        contentItems.Add(new OpenAIInputContent
                        {
                            Type = "input_image",
                            ImageUrl = GetImageDataUrl(imgBytes),
                            Detail = "auto"
                        });
                    }
                }
            }

            responsesRequest.Input = new List<OpenAIInputMessage>
            {
                new() { Role = "user", Content = contentItems }
            };

            var jsonContent = JsonSerializer.Serialize(responsesRequest, jsonOptions);
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

            var genResponse = JsonSerializer.Deserialize<OpenAIResponsesResponse>(responseContent, jsonOptions);
            if (genResponse == null)
                throw new InvalidOperationException("Failed to parse Responses API output.");

            byte[]? imageBytes = null;
            if (genResponse.Output != null)
            {
                foreach (var output in genResponse.Output)
                {
                    if (output.Type == "image_generation_call" && !string.IsNullOrWhiteSpace(output.Result))
                    {
                        imageBytes = Convert.FromBase64String(output.Result);
                        break;
                    }
                }
            }

            if (imageBytes == null || imageBytes.Length == 0)
                throw new InvalidOperationException("No image returned from generation API.");

            return new ImageGenerationResult
            {
                ImageBytes = imageBytes,
                ResponseId = genResponse.Id,
                InputTokens = genResponse.Usage?.InputTokens ?? 0,
                OutputTokens = genResponse.Usage?.OutputTokens ?? 0
            };
        }

        static string GetImageDataUrl(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0)
                return "";

            var format = Image.DetectFormat(imageData);
            var mime = format?.DefaultMimeType ?? "image/png";
            return $"data:{mime};base64,{Convert.ToBase64String(imageData)}";
        }

        static readonly (int W, int H)[] SupportedResolutions =
        {
            (1024, 1024),
            (1536, 1024),
            (1024, 1536),
            (2048, 2048),
            (2048, 1152),
            (3840, 2160),
            (2160, 3840),
        };

        public static string FindBestResolution(string requestedSize)
        {
            var parts = requestedSize.Split('x');
            if (parts.Length != 2 || !int.TryParse(parts[0], out var targetW) || !int.TryParse(parts[1], out var targetH))
                return "1024x1024";

            var targetRatio = (double)targetW / targetH;
            var targetPixels = (long)targetW * targetH;

            var best = SupportedResolutions[0];
            var bestScore = double.MaxValue;

            foreach (var (w, h) in SupportedResolutions)
            {
                var ratio = (double)w / h;
                var ratioDiff = Math.Abs(ratio - targetRatio);
                var pixels = (long)w * h;
                var pixelDiff = Math.Abs(pixels - targetPixels);

                var score = ratioDiff * 1000 + pixelDiff / 1_000_000.0;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = (w, h);
                }
            }

            return $"{best.W}x{best.H}";
        }
    }
}

