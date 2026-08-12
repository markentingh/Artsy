using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Artsy.API.Models.Projects;
using Artsy.Data.Entities;
using Microsoft.Extensions.Options;

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

        public string ModelKey => "openai";

        public IImageTokens CreateTokenizer(ImageGenerationModel model)
        {
            return new ImageTokensForOpenAI(model.CPMITTokens, model.CPMIITokens, model.CPMOTokens);
        }

        public ImageGenerationForOpenAI(IHttpClientFactory httpClientFactory, IOptions<ImageGenerationOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
        }

        public async Task<ImageGenerationResult> GenerateAsync(ImageGenerationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Prompt))
                throw new ArgumentException("Prompt is required.", nameof(request));

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

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var imageApiRequest = new
            {
                model,
                prompt = request.Prompt,
                n = 1,
                size,
                quality
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

            using var multipart = new MultipartFormDataContent();

            multipart.Add(new StringContent(model), "model");
            multipart.Add(new StringContent(request.Prompt), "prompt");
            multipart.Add(new StringContent(size), "size");
            multipart.Add(new StringContent(quality), "quality");

            var firstImage = request.InputImages[0];
            var imageContent = new ByteArrayContent(firstImage);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            multipart.Add(imageContent, "image", "image.png");

            if (request.InputMask != null && request.InputMask.Length > 0)
            {
                var maskContent = new ByteArrayContent(request.InputMask);
                maskContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                multipart.Add(maskContent, "mask", "mask.png");
            }

            for (var i = 1; i < request.InputImages.Count; i++)
            {
                var extraContent = new ByteArrayContent(request.InputImages[i]);
                extraContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                multipart.Add(extraContent, $"image[{i}]", $"image_{i}.png");
            }

            using var client = _httpClientFactory.CreateClient("ImageGeneration");
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, config.ImageEditEndpoint)
            {
                Content = multipart
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));
            var response = await client.SendAsync(httpRequest, cts.Token);
            var responseContent = await response.Content.ReadAsStringAsync(cts.Token);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Image edit failed: {response.StatusCode} - {responseContent}");

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

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
                            ImageUrl = $"data:image/png;base64,{Convert.ToBase64String(imgBytes)}",
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

