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
            var size = !string.IsNullOrWhiteSpace(request.CustomSize) ? request.CustomSize : FindBestResolution($"{request.Width}x{request.Height}");
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
            var size = !string.IsNullOrWhiteSpace(request.CustomSize) ? request.CustomSize : FindBestResolution($"{request.Width}x{request.Height}");
            var quality = string.IsNullOrWhiteSpace(request.Quality) ? "medium" : request.Quality;

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            using var client = _httpClientFactory.CreateClient("ImageGeneration");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            HttpResponseMessage response;

            // When a mask is provided, use multipart form data (image + mask as file uploads)
            if (request.InputMask != null && request.InputMask.Length > 0 && request.InputImages.Count > 0)
            {
                using var formContent = new MultipartFormDataContent();

                formContent.Add(new StringContent(model), "model");
                formContent.Add(new StringContent(request.Prompt), "prompt");
                formContent.Add(new StringContent("1"), "n");
                formContent.Add(new StringContent(size), "size");
                formContent.Add(new StringContent(quality), "quality");

                // First input image as the base "image" file
                var baseImage = request.InputImages[0];
                var imageContent = new ByteArrayContent(baseImage);
                imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                formContent.Add(imageContent, "image", "image.png");

                // Mask as the "mask" file
                var maskContent = new ByteArrayContent(request.InputMask);
                maskContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                formContent.Add(maskContent, "mask", "mask.png");

                // Additional input images as extra file fields
                for (var i = 1; i < request.InputImages.Count; i++)
                {
                    var extraImg = request.InputImages[i];
                    if (extraImg == null || extraImg.Length == 0) continue;
                    var extraContent = new ByteArrayContent(extraImg);
                    extraContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                    formContent.Add(extraContent, "image[]", $"image_{i}.png");
                }

                using var maskRequest = new HttpRequestMessage(HttpMethod.Post, config.ImageEditEndpoint)
                {
                    Content = formContent
                };
                maskRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
                response = await client.SendAsync(maskRequest, cts.Token);
            }
            else
            {
                // No mask: use JSON body with image URLs (existing behavior)
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

                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, config.ImageEditEndpoint)
                {
                    Content = content
                };
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
                response = await client.SendAsync(httpRequest, cts.Token);
            }

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
            var toolSize = !string.IsNullOrWhiteSpace(request.CustomSize) ? request.CustomSize : FindBestResolution($"{request.Width}x{request.Height}");
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

        /// <summary>
        /// Calculates a valid GPT image 2.0 custom size for a placement with the given dimensions.
        /// GPT image 2.0 supports custom sizes where edges are multiples of 16 and ratio ≤ 3:1.
        /// The returned size matches the placement's aspect ratio (clamped to 3:1) at a generation
        /// resolution appropriate for upscaling later.
        /// </summary>
        /// <param name="placementWidth">Placement print width in pixels</param>
        /// <param name="placementHeight">Placement print height in pixels</param>
        /// <returns>Tuple of (width, height, needsCrop) where needsCrop is true when the placement
        /// ratio exceeds 3:1 and the generated image will need post-generation cropping</returns>
        public static (int Width, int Height, bool NeedsCrop) CalculateCustomResolution(int placementWidth, int placementHeight)
        {
            if (placementWidth <= 0 || placementHeight <= 0)
                return (1024, 1024, false);

            var ratio = (double)placementWidth / placementHeight;
            var needsCrop = Math.Abs(ratio) > 3.0 || Math.Abs(ratio) < 1.0 / 3.0;

            // Clamp ratio to 3:1 for generation
            double genRatio;
            if (ratio > 3.0)
                genRatio = 3.0;
            else if (ratio < 1.0 / 3.0)
                genRatio = 1.0 / 3.0;
            else
                genRatio = ratio;

            // Pick target area based on the larger placement dimension
            // 2K area (~4M pixels) for placements > 1024px, 1K area (~1M pixels) for smaller
            var maxDim = Math.Max(placementWidth, placementHeight);
            var targetArea = maxDim > 1024 ? 2048.0 * 2048 : 1024.0 * 1024;

            // Calculate width and height from target area and ratio
            var w = Math.Sqrt(targetArea * genRatio);
            var h = Math.Sqrt(targetArea / genRatio);

            // Round to nearest multiple of 16
            var width = (int)Math.Round(w / 16) * 16;
            var height = (int)Math.Round(h / 16) * 16;

            // Ensure minimum dimensions
            if (width < 64) width = 64;
            if (height < 64) height = 64;

            return (width, height, needsCrop);
        }

        /// <summary>
        /// Parses an aspect ratio string (e.g. "9:16", "1:1", "16:9") and returns pixel dimensions
        /// at the requested resolution tier. 1K targets ~1M pixels, 2K targets ~4M pixels.
        /// Dimensions are rounded to multiples of 16 for GPT image 2.0 compatibility.
        /// </summary>
        public static (int Width, int Height) GetDimensionsFromAspectRatio(string aspectRatio, int tier)
        {
            if (string.IsNullOrWhiteSpace(aspectRatio))
                aspectRatio = "1:1";

            var parts = aspectRatio.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[0], out var rw) || !int.TryParse(parts[1], out var rh) || rw <= 0 || rh <= 0)
                return (tier, tier);

            var ratio = (double)rw / rh;
            var targetArea = tier == 1 ? 1024.0 * 1024 : 2048.0 * 2048;

            var w = Math.Sqrt(targetArea * ratio);
            var h = Math.Sqrt(targetArea / ratio);

            var width = (int)Math.Round(w / 16) * 16;
            var height = (int)Math.Round(h / 16) * 16;

            if (width < 64) width = 64;
            if (height < 64) height = 64;

            return (width, height);
        }
    }
}

