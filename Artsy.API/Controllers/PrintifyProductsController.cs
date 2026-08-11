using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Artsy.API.Models;
using Artsy.API.Models.Collections;
using Artsy.API.Models.Printify;
using Artsy.API.Models.Projects;
using Artsy.API.Services;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Entities;
using Artsy.Data.Interfaces.Projects;
using Artsy.Data.Interfaces;

namespace Artsy.API.Controllers
{
    [Route("/api/printify-products")]
    [Authorize]
    public class PrintifyProductsController : ApiController
    {
        readonly IPrintifyService _printifyService;
        readonly IProjectCollectionRepository _projectCollectionRepository;
        readonly IProjectRepository _projectRepository;
        readonly IProjectCollectionPrintifyProductRepository _printifyProductRepository;
        readonly IProjectCollectionProductRepository _productRepository;
        readonly IProjectBlueprintsRepository _blueprintRepository;
        readonly IProjectCollectionProductImageRepository _productImageRepository;
        readonly IProjectCollectionArtworkRepository _artworkRepository;
        readonly IPrintifyBlueprintImageRepository _printifyBlueprintImageRepository;
        readonly IPrintifyBlueprintImageVariantRepository _printifyBlueprintImageVariantRepository;
        readonly IProjectBlueprintProductImageRepository _blueprintProductImageRepository;
        readonly IProjectCollectionPrintifyProductMockupRepository _mockupRepository;
        readonly IImageService _imageService;
        readonly IHttpClientFactory _httpClientFactory;

        public PrintifyProductsController(
            IPrintifyService printifyService,
            IProjectCollectionRepository projectCollectionRepository,
            IProjectRepository projectRepository,
            IProjectCollectionPrintifyProductRepository printifyProductRepository,
            IProjectCollectionProductRepository productRepository,
            IProjectBlueprintsRepository blueprintRepository,
            IProjectCollectionProductImageRepository productImageRepository,
            IProjectCollectionArtworkRepository artworkRepository,
            IPrintifyBlueprintImageRepository printifyBlueprintImageRepository,
            IPrintifyBlueprintImageVariantRepository printifyBlueprintImageVariantRepository,
            IProjectBlueprintProductImageRepository blueprintProductImageRepository,
            IProjectCollectionPrintifyProductMockupRepository mockupRepository,
            IImageService imageService,
            IHttpClientFactory httpClientFactory)
        {
            _printifyService = printifyService;
            _projectCollectionRepository = projectCollectionRepository;
            _projectRepository = projectRepository;
            _printifyProductRepository = printifyProductRepository;
            _productRepository = productRepository;
            _blueprintRepository = blueprintRepository;
            _productImageRepository = productImageRepository;
            _artworkRepository = artworkRepository;
            _printifyBlueprintImageRepository = printifyBlueprintImageRepository;
            _printifyBlueprintImageVariantRepository = printifyBlueprintImageVariantRepository;
            _blueprintProductImageRepository = blueprintProductImageRepository;
            _mockupRepository = mockupRepository;
            _imageService = imageService;
            _httpClientFactory = httpClientFactory;
        }

        private static string GetPositionLabel(int position) => position switch
        {
            1 => "front",
            2 => "back",
            3 => "top",
            4 => "bottom",
            5 => "left",
            6 => "right",
            _ => "front"
        };

        private async Task<bool> ValidateAccess(Guid collectionId, Guid productId)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return false;

            var collection = await _projectCollectionRepository.GetByIdAsync(collectionId);
            if (collection == null || collection.Status != 1)
                return false;

            var project = await _projectRepository.GetByIdAsync(collection.ProjectId, userId);
            if (project == null)
                return false;

            return true;
        }

        [HttpPost("upload-artwork-image")]
        public async Task<IActionResult> UploadArtworkImage([FromBody] UploadArtworkImageRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.CollectionId == Guid.Empty || request.ArtworkId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "CollectionId and ArtworkId are required." });

            try
            {
                var collection = await _projectCollectionRepository.GetByIdAsync(request.CollectionId);
                if (collection == null || collection.Status != 1)
                    return Json(new ApiResponse { success = false, message = "Collection not found." });

                var project = await _projectRepository.GetByIdAsync(collection.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Collection not found." });

                var shopId = project.PrintifyStoreId ?? 0;
                if (shopId == 0)
                    return Json(new ApiResponse { success = false, message = "No Printify store selected for this project." });

                var artwork = await _artworkRepository.GetByIdAsync(request.CollectionId, request.ArtworkId);
                if (artwork == null)
                    return Json(new ApiResponse { success = false, message = "Artwork not found." });

                if (!string.IsNullOrWhiteSpace(artwork.PrintifyImageId))
                    return Json(new ApiResponse { success = true, data = new { printifyImageId = artwork.PrintifyImageId } });

                byte[] imgBytes;
                if (artwork.Opacity)
                {
                    // For opacity artworks, upload the transparent PNG to Printify
                    imgBytes = await _imageService.GetProjectCollectionArtworkFullSizePngAsync(
                        artwork.ProjectId, request.CollectionId, artwork.ItemId, artwork.Id);
                    if (imgBytes == null || imgBytes.Length == 0)
                    {
                        imgBytes = await _imageService.GetProjectCollectionArtworkPngAsync(
                            artwork.ProjectId, request.CollectionId, artwork.ItemId, artwork.Id);
                    }
                }
                else
                {
                    imgBytes = await _imageService.GetProjectCollectionArtworkFullSizeAsync(
                        artwork.ProjectId, request.CollectionId, artwork.ItemId, artwork.Id);
                    if (imgBytes == null || imgBytes.Length == 0)
                    {
                        imgBytes = await _imageService.GetProjectCollectionArtworkImageAsync(
                            artwork.ProjectId, request.CollectionId, artwork.ItemId, artwork.Id);
                    }
                }
                if (imgBytes == null || imgBytes.Length == 0)
                    return Json(new ApiResponse { success = false, message = "Artwork file not found." });

                var cropSettings = await GetCropSettingsForArtworkAsync(collection.ProjectId, artwork.ItemId);
                if (cropSettings != null)
                    imgBytes = CropImage(imgBytes, cropSettings.Value.Width, cropSettings.Value.Height, cropSettings.Value.CropX, cropSettings.Value.CropY, artwork.Opacity);

                var base64 = Convert.ToBase64String(imgBytes);
                var fileName = artwork.Opacity ? $"{artwork.Id}.png" : $"{artwork.Id}.jpg";
                var uploadResp = await _printifyService.UploadImageAsync(userId, fileName, base64);
                if (uploadResp == null)
                    return Json(new ApiResponse { success = false, message = "Failed to upload artwork to Printify." });

                await _artworkRepository.SetPrintifyImageIdAsync(artwork.Id, uploadResp.Id);

                return Json(new ApiResponse { success = true, data = new { printifyImageId = uploadResp.Id } });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        private async Task<(int Width, int Height, string CropX, string CropY)?> GetCropSettingsForArtworkAsync(Guid projectId, Guid itemId)
        {
            var blueprints = await _blueprintRepository.GetByProjectIdAsync(projectId);
            foreach (var bp in blueprints)
            {
                if (string.IsNullOrWhiteSpace(bp.PlacementJson)) continue;
                try
                {
                    var placements = JsonSerializer.Deserialize<List<PlacementDto>>(bp.PlacementJson);
                    if (placements == null) continue;
                    foreach (var p in placements)
                    {
                        if (p.GetItemId() == itemId)
                        {
                            var (w, h) = p.GetDimensions();
                            if (w > 0 && h > 0)
                                return (w, h, p.CropX ?? "center", p.CropY ?? "center");
                        }
                    }
                }
                catch { }
            }
            return null;
        }

        private static byte[] CropImage(byte[] imgBytes, int targetWidth, int targetHeight, string cropX, string cropY, bool preservePng = false)
        {
            using var image = Image.Load(imgBytes);
            var srcW = image.Width;
            var srcH = image.Height;
            var targetRatio = (double)targetWidth / targetHeight;
            var srcRatio = (double)srcW / srcH;

            int cropW, cropH, cropXPos, cropYPos;

            if (srcRatio > targetRatio)
            {
                cropH = srcH;
                cropW = (int)(srcH * targetRatio);
                cropYPos = 0;
                cropXPos = cropX.ToLower() switch
                {
                    "left" => 0,
                    "right" => srcW - cropW,
                    _ => (srcW - cropW) / 2,
                };
            }
            else if (srcRatio < targetRatio)
            {
                cropW = srcW;
                cropH = (int)(srcW / targetRatio);
                cropXPos = 0;
                cropYPos = cropY.ToLower() switch
                {
                    "top" => 0,
                    "bottom" => srcH - cropH,
                    _ => (srcH - cropH) / 2,
                };
            }
            else
            {
                return imgBytes;
            }

            image.Mutate(ctx => ctx.Crop(new Rectangle(cropXPos, cropYPos, cropW, cropH)));
            using var ms = new MemoryStream();
            if (preservePng)
                image.Save(ms, new SixLabors.ImageSharp.Formats.Png.PngEncoder());
            else
                image.Save(ms, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder());
            return ms.ToArray();
        }

        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] CreatePrintifyProductRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.CollectionId == Guid.Empty || request.ProjectBlueprintId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "CollectionId and ProjectBlueprintId are required." });

            try
            {
                var collection = await _projectCollectionRepository.GetByIdAsync(request.CollectionId);
                if (collection == null || collection.Status != 1)
                    return Json(new ApiResponse { success = false, message = "Collection not found." });

                var project = await _projectRepository.GetByIdAsync(collection.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Collection not found." });

                var shopId = project.PrintifyStoreId ?? 0;
                if (shopId == 0)
                    return Json(new ApiResponse { success = false, message = "No Printify store selected for this project." });

                var bp = await _blueprintRepository.GetByIdAsync(request.ProjectBlueprintId);
                if (bp == null)
                    return Json(new ApiResponse { success = false, message = "Blueprint not found." });

                var product = await _productRepository.GetByCollectionAndBlueprintIdAsync(request.CollectionId, bp.Id);
                if (product == null)
                {
                    product = await _productRepository.CreateAsync(new ProjectCollectionProduct
                    {
                        ProjectId = collection.ProjectId,
                        CollectionId = request.CollectionId,
                        ProjectBlueprintId = bp.Id,
                        BlueprintId = bp.BlueprintId,
                        Name = bp.Name,
                        Description = bp.Description ?? "",
                        SafetyInfo = bp.SafetyInfo ?? "",
                        PricingJson = bp.PricingJson ?? "[]"
                    });
                }

                if (bp.PrintProviderId == 0)
                    return Json(new ApiResponse { success = false, message = "No print provider configured for blueprint." });
                var printProviderId = bp.PrintProviderId;

                var variantIds = new List<int>();
                if (!string.IsNullOrWhiteSpace(bp.BlueprintJson))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(bp.BlueprintJson);
                        if (doc.RootElement.TryGetProperty("variantIds", out var vIdsEl) && vIdsEl.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var v in vIdsEl.EnumerateArray())
                            {
                                if (v.TryGetInt32(out var vId))
                                    variantIds.Add(vId);
                            }
                        }
                    }
                    catch { }
                }

                if (variantIds.Count == 0)
                    return Json(new ApiResponse { success = false, message = "No variants configured for blueprint." });

                var priceMap = new Dictionary<int, int>();
                if (!string.IsNullOrWhiteSpace(bp.PricingJson))
                {
                    try
                    {
                        var pricing = JsonSerializer.Deserialize<List<JsonElement>>(bp.PricingJson);
                        if (pricing != null)
                        {
                            foreach (var p in pricing)
                            {
                                if (p.TryGetProperty("variantId", out var vidEl) && p.TryGetProperty("price", out var priceEl))
                                    priceMap[vidEl.GetInt32()] = (int)Math.Round(priceEl.GetDecimal() * 100);
                            }
                        }
                    }
                    catch { }
                }

                var variants = variantIds.Select(vid => new PrintifyVariantRequest
                {
                    Id = vid,
                    Price = priceMap.TryGetValue(vid, out var price) ? price : 0,
                    IsEnabled = true,
                }).ToList();

                var collectionArtwork = (await _artworkRepository.GetByCollectionIdAsync(request.CollectionId))
                    .Where(a => a.Accepted && a.Active && !string.IsNullOrWhiteSpace(a.PrintifyImageId))
                    .ToList();

                var productImages = (await _productImageRepository.GetByCollectionIdAsync(request.CollectionId))
                    .Where(img => img.ProjectBlueprintId == bp.Id && img.Accepted && img.Active)
                    .ToList();

                var printAreas = new List<PrintifyPrintAreaRequest>();
                var artworkUsedInPlacements = new HashSet<Guid>();
                if (!string.IsNullOrWhiteSpace(bp.PlacementJson) && collectionArtwork.Count > 0)
                {
                    try
                    {
                        var placements = JsonSerializer.Deserialize<List<PlacementDto>>(bp.PlacementJson);
                        if (placements != null)
                        {
                            foreach (var placement in placements)
                            {
                                if (string.IsNullOrWhiteSpace(placement.Source)) continue;

                                var itemId = placement.GetItemId();
                                if (itemId == Guid.Empty) continue;

                                var art = collectionArtwork.FirstOrDefault(a => a.ItemId == itemId);
                                if (art == null) continue;

                                artworkUsedInPlacements.Add(art.Id);

                                var position = (placement.Position ?? "").ToLower();
                                printAreas.Add(new PrintifyPrintAreaRequest
                                {
                                    VariantIds = variantIds,
                                    Placeholders = new List<PrintifyPlaceholderRequest>
                                    {
                                        new PrintifyPlaceholderRequest
                                        {
                                            Position = position,
                                            Images = new List<PrintifyPlaceholderImageRequest>
                                            {
                                                new PrintifyPlaceholderImageRequest
                                                {
                                                    Id = art.PrintifyImageId,
                                                    X = 0.5,
                                                    Y = 0.5,
                                                    Scale = 1,
                                                    Angle = 0,
                                                }
                                            },
                                        }
                                    }
                                });
                            }
                        }
                    }
                    catch { }
                }

                var blueprintImages = (await _printifyBlueprintImageRepository.GetByBlueprintIdAsync(bp.BlueprintId)).ToList();
                var blueprintImageIds = blueprintImages.Select(bi => bi.Id).ToList();
                var imageVariants = blueprintImageIds.Count > 0
                    ? (await _printifyBlueprintImageVariantRepository.GetByBlueprintImageIdsAsync(blueprintImageIds)).ToList()
                    : new List<PrintifyBlueprintImageVariant>();

                var productImageIds = productImages.Select(pi => pi.ProductImageId).Where(id => id != Guid.Empty).ToList();
                var blueprintProductImages = productImageIds.Count > 0
                    ? (await _blueprintProductImageRepository.GetByBlueprintIdsAsync(new[] { bp.Id })).ToDictionary(bpi => bpi.Id)
                    : new Dictionary<Guid, ProjectBlueprintProductImage>();

                var positionByVariantColor = new Dictionary<string, int>();
                foreach (var bi in blueprintImages)
                {
                    var variantsForImage = imageVariants.Where(v => v.BlueprintImageId == bi.Id);
                    foreach (var v in variantsForImage)
                    {
                        positionByVariantColor[v.VariantColor] = bi.Position;
                    }
                }

                var domain = ConnectionSettings.PrintifyImagesDomain;
                if (!string.IsNullOrWhiteSpace(domain) && !domain.EndsWith("/"))
                    domain += "/";

                var images = new List<PrintifyProductImageRequest>();
                foreach (var img in productImages)
                {
                    var position = "front";
                    if (img.ProductImageId != Guid.Empty && blueprintProductImages.TryGetValue(img.ProductImageId, out var bpi))
                    {
                        if (positionByVariantColor.TryGetValue(bpi.VariantColor, out var pos))
                            position = GetPositionLabel(pos);
                    }

                    images.Add(new PrintifyProductImageRequest
                    {
                        Src = $"{domain}printify/image/product/{img.Id}",
                        VariantIds = variantIds,
                        Position = position,
                        IsDefault = false,
                    });
                }

                foreach (var art in collectionArtwork.Where(a => artworkUsedInPlacements.Contains(a.Id)))
                {
                    images.Add(new PrintifyProductImageRequest
                    {
                        Src = $"{domain}printify/image/artwork/{art.Id}",
                        VariantIds = variantIds,
                        Position = "front",
                        IsDefault = false,
                    });
                }

                var description = bp.Description ?? "";
                if (!string.IsNullOrWhiteSpace(description))
                    description += "\n\n";
                description += "Disclaimer: The artworks printed on this product were generated using AI. The products and any humans and environments within the mockup images were also generated using AI. The real-world product may appear slightly different from these mockup images as a result.";

                var productRequest = new PrintifyProductRequest
                {
                    Title = !string.IsNullOrWhiteSpace(product.Name) ? product.Name : bp.Name,
                    Description = description,
                    SafetyInformation = bp.SafetyInfo ?? "",
                    BlueprintId = bp.BlueprintId,
                    PrintProviderId = printProviderId,
                    Variants = variants,
                    PrintAreas = printAreas,
                    Images = images,
                };

                var requestJson = JsonSerializer.Serialize(productRequest, new JsonSerializerOptions { WriteIndented = false });

                var result = await _printifyService.CreateProductAsync(userId, shopId, productRequest);
                if (result == null)
                    return Json(new ApiResponse { success = false, message = "Failed to create product on Printify." });
                if (!result.Success)
                    return Json(new ApiResponse { success = false, message = result.Error });

                var response = result.Product;

                var existing = await _printifyProductRepository.GetByCollectionAndProductIdAsync(request.CollectionId, product.Id);
                if (existing != null)
                {
                    existing.PrintifyProductId = response.Id;
                    existing.PrintifyShopId = response.ShopId;
                    existing.PrintifyUserId = response.UserId;
                    existing.ProviderId = response.PrintProviderId;
                    existing.Published = false;
                    existing.Status = 1;
                    existing.RequestJson = requestJson;
                    await _printifyProductRepository.UpdateAsync(existing);

                    var mockupsDownloaded = await DownloadAndSaveMockupsAsync(userId, shopId, response.Id, collection.ProjectId, request.CollectionId, existing.Id);

                    return Json(new ApiResponse { success = true, data = new
                    {
                        existing.Id,
                        existing.ProjectId,
                        existing.CollectionId,
                        existing.ProductId,
                        existing.PrintifyProductId,
                        existing.PrintifyShopId,
                        existing.PrintifyUserId,
                        existing.ProviderId,
                        existing.Published,
                        existing.Status,
                        existing.Created,
                        ProjectBlueprintId = product.ProjectBlueprintId,
                        BlueprintName = product.Name,
                        MockupsDownloaded = mockupsDownloaded,
                    } });
                }

                var record = await _printifyProductRepository.CreateAsync(new ProjectCollectionPrintifyProduct
                {
                    ProjectId = collection.ProjectId,
                    CollectionId = request.CollectionId,
                    ProductId = product.Id,
                    PrintifyProductId = response.Id,
                    PrintifyShopId = response.ShopId,
                    PrintifyUserId = response.UserId,
                    ProviderId = response.PrintProviderId,
                    Published = false,
                    Status = 1,
                    RequestJson = requestJson
                });

                var mockupsDownloaded2 = await DownloadAndSaveMockupsAsync(userId, shopId, response.Id, collection.ProjectId, request.CollectionId, record.Id);

                return Json(new ApiResponse { success = true, data = new
                {
                    record.Id,
                    record.ProjectId,
                    record.CollectionId,
                    record.ProductId,
                    record.PrintifyProductId,
                    record.PrintifyShopId,
                    record.PrintifyUserId,
                    record.ProviderId,
                    record.Published,
                    record.Status,
                    record.Created,
                    ProjectBlueprintId = product.ProjectBlueprintId,
                    BlueprintName = product.Name,
                    MockupsDownloaded = mockupsDownloaded2,
                } });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("download-mockups")]
        public async Task<IActionResult> DownloadMockups([FromBody] DownloadMockupsRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.CollectionId == Guid.Empty || request.ProjectBlueprintId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "CollectionId and ProjectBlueprintId are required." });

            try
            {
                var collection = await _projectCollectionRepository.GetByIdAsync(request.CollectionId);
                if (collection == null || collection.Status != 1)
                    return Json(new ApiResponse { success = false, message = "Collection not found." });

                var project = await _projectRepository.GetByIdAsync(collection.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Collection not found." });

                var shopId = project.PrintifyStoreId ?? 0;
                if (shopId == 0)
                    return Json(new ApiResponse { success = false, message = "No Printify store selected for this project." });

                var product = await _productRepository.GetByCollectionAndBlueprintIdAsync(request.CollectionId, request.ProjectBlueprintId);
                if (product == null)
                    return Json(new ApiResponse { success = false, message = "Product not found." });

                var printifyProduct = await _printifyProductRepository.GetByCollectionAndProductIdAsync(request.CollectionId, product.Id);
                if (printifyProduct == null || string.IsNullOrWhiteSpace(printifyProduct.PrintifyProductId))
                    return Json(new ApiResponse { success = false, message = "Printify product not found." });

                var mockupsDownloaded = await DownloadAndSaveMockupsAsync(userId, shopId, printifyProduct.PrintifyProductId, collection.ProjectId, request.CollectionId, printifyProduct.Id);

                return Json(new ApiResponse { success = true, data = new
                {
                    printifyProduct.Id,
                    printifyProduct.ProjectId,
                    printifyProduct.CollectionId,
                    printifyProduct.ProductId,
                    printifyProduct.PrintifyProductId,
                    printifyProduct.PrintifyShopId,
                    printifyProduct.PrintifyUserId,
                    printifyProduct.ProviderId,
                    printifyProduct.Published,
                    printifyProduct.Status,
                    printifyProduct.Created,
                    ProjectBlueprintId = product.ProjectBlueprintId,
                    BlueprintName = product.Name,
                    MockupsDownloaded = mockupsDownloaded,
                } });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] UpdatePrintifyProductRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.CollectionId == Guid.Empty || request.ProductId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "CollectionId and ProductId are required." });

            if (!await ValidateAccess(request.CollectionId, request.ProductId))
                return Json(new ApiResponse { success = false, message = "Collection or product not found." });

            try
            {
                var record = await _printifyProductRepository.GetByCollectionAndProductIdAsync(request.CollectionId, request.ProductId);
                if (record == null)
                    return Json(new ApiResponse { success = false, message = "Printify product not found." });

                var response = await _printifyService.UpdateProductAsync(userId, record.PrintifyShopId, record.PrintifyProductId, null!);
                if (response == null)
                    return Json(new ApiResponse { success = false, message = "Failed to update product on Printify." });

                return Json(new ApiResponse { success = true, data = response });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("publish")]
        public async Task<IActionResult> Publish([FromBody] PublishPrintifyProductRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.CollectionId == Guid.Empty || request.ProductId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "CollectionId and ProductId are required." });

            if (!await ValidateAccess(request.CollectionId, request.ProductId))
                return Json(new ApiResponse { success = false, message = "Collection or product not found." });

            try
            {
                var record = await _printifyProductRepository.GetByCollectionAndProductIdAsync(request.CollectionId, request.ProductId);
                if (record == null)
                    return Json(new ApiResponse { success = false, message = "Printify product not found." });

                var publishRequest = new PrintifyPublishRequest
                {
                    Title = true,
                    Description = true,
                    Images = true,
                    Variants = true,
                    Tags = true,
                    KeyFeatures = true,
                    ShippingTemplate = true,
                };

                var success = await _printifyService.PublishProductAsync(userId, record.PrintifyShopId, record.PrintifyProductId, publishRequest);
                if (!success)
                    return Json(new ApiResponse { success = false, message = "Failed to publish product on Printify." });

                await _printifyProductRepository.SetPublishedAsync(record.Id, true);
                record.Published = true;
                return Json(new ApiResponse { success = true, data = record });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("unpublish")]
        public async Task<IActionResult> Unpublish([FromBody] UnpublishPrintifyProductRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.CollectionId == Guid.Empty || request.ProductId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "CollectionId and ProductId are required." });

            if (!await ValidateAccess(request.CollectionId, request.ProductId))
                return Json(new ApiResponse { success = false, message = "Collection or product not found." });

            try
            {
                var record = await _printifyProductRepository.GetByCollectionAndProductIdAsync(request.CollectionId, request.ProductId);
                if (record == null)
                    return Json(new ApiResponse { success = false, message = "Printify product not found." });

                var success = await _printifyService.UnpublishProductAsync(userId, record.PrintifyShopId, record.PrintifyProductId);
                if (!success)
                    return Json(new ApiResponse { success = false, message = "Failed to unpublish product on Printify." });

                await _printifyProductRepository.SetPublishedAsync(record.Id, false);
                record.Published = false;
                return Json(new ApiResponse { success = true, data = record });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("delete")]
        public async Task<IActionResult> Delete([FromBody] DeletePrintifyProductRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.CollectionId == Guid.Empty || request.ProductId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "CollectionId and ProductId are required." });

            if (!await ValidateAccess(request.CollectionId, request.ProductId))
                return Json(new ApiResponse { success = false, message = "Collection or product not found." });

            try
            {
                var record = await _printifyProductRepository.GetByCollectionAndProductIdAsync(request.CollectionId, request.ProductId);
                if (record == null)
                    return Json(new ApiResponse { success = false, message = "Printify product not found." });

                var success = await _printifyService.DeleteProductAsync(userId, record.PrintifyShopId, record.PrintifyProductId);
                if (!success)
                    return Json(new ApiResponse { success = false, message = "Failed to delete product on Printify." });

                await _printifyProductRepository.DeleteAsync(record.Id);
                return Json(new ApiResponse { success = true });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("get-by-collection")]
        public async Task<IActionResult> GetByCollection([FromQuery] Guid collectionId)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (collectionId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "CollectionId is required." });

            try
            {
                var collection = await _projectCollectionRepository.GetByIdAsync(collectionId);
                if (collection == null || collection.Status != 1)
                    return Json(new ApiResponse { success = false, message = "Collection not found." });

                var project = await _projectRepository.GetByIdAsync(collection.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Collection not found." });

                var printifyProducts = await _printifyProductRepository.GetByCollectionIdAsync(collectionId);
                var products = await _productRepository.GetByCollectionIdAsync(collectionId);
                var mockups = await _mockupRepository.GetByCollectionIdAsync(collectionId);
                var mockupsByPrintifyProductId = mockups.GroupBy(m => m.PrintifyProductId)
                    .ToDictionary(g => g.Key, g => g.Count());

                var productMap = products.ToDictionary(p => p.Id);
                var result = printifyProducts.Select(pp => new
                {
                    pp.Id,
                    pp.ProjectId,
                    pp.CollectionId,
                    pp.ProductId,
                    pp.PrintifyProductId,
                    pp.PrintifyShopId,
                    pp.PrintifyUserId,
                    pp.ProviderId,
                    pp.Published,
                    pp.Status,
                    pp.Created,
                    ProjectBlueprintId = productMap.TryGetValue(pp.ProductId, out var p) ? p.ProjectBlueprintId : Guid.Empty,
                    BlueprintName = productMap.TryGetValue(pp.ProductId, out p) ? p.Name : "",
                    MockupsDownloaded = mockupsByPrintifyProductId.TryGetValue(pp.Id, out var mockupCount) && mockupCount > 0,
                });

                return Json(new ApiResponse { success = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("get-mockups")]
        public async Task<IActionResult> GetMockups([FromQuery] Guid collectionId)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (collectionId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "CollectionId is required." });

            try
            {
                var collection = await _projectCollectionRepository.GetByIdAsync(collectionId);
                if (collection == null || collection.Status != 1)
                    return Json(new ApiResponse { success = false, message = "Collection not found." });

                var project = await _projectRepository.GetByIdAsync(collection.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Collection not found." });

                var mockups = await _mockupRepository.GetByCollectionIdAsync(collectionId);
                var result = mockups.Select(m => new
                {
                    m.Id,
                    m.ProjectId,
                    m.CollectionId,
                    m.PrintifyProductId,
                    m.VariantIds,
                    m.Position,
                    m.IsDefault,
                    m.Status,
                    ImageUrl = $"/api/printify-products/mockup-image?projectId={m.ProjectId}&collectionId={m.CollectionId}&mockupId={m.Id}&thumb=true",
                });

                return Json(new ApiResponse { success = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("mockup-image")]
        public async Task<IActionResult> GetMockupImage([FromQuery] Guid projectId, [FromQuery] Guid collectionId, [FromQuery] Guid mockupId, [FromQuery] bool thumb = false)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (projectId == Guid.Empty || collectionId == Guid.Empty || mockupId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "projectId, collectionId, and mockupId are required." });

            try
            {
                var imgBytes = thumb
                    ? await _imageService.GetProjectCollectionMockupThumbAsync(projectId, collectionId, mockupId)
                    : await _imageService.GetProjectCollectionMockupAsync(projectId, collectionId, mockupId);
                if (imgBytes == null || imgBytes.Length == 0)
                    return NotFound();

                return File(imgBytes, "image/jpeg");
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpGet("get-products")]
        public async Task<IActionResult> GetProducts([FromQuery] Guid collectionId)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (collectionId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "CollectionId is required." });

            try
            {
                var collection = await _projectCollectionRepository.GetByIdAsync(collectionId);
                if (collection == null || collection.Status != 1)
                    return Json(new ApiResponse { success = false, message = "Collection not found." });

                var project = await _projectRepository.GetByIdAsync(collection.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Collection not found." });

                var products = await _productRepository.GetByCollectionIdAsync(collectionId);
                return Json(new ApiResponse { success = true, data = products });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("ensure-products")]
        public async Task<IActionResult> EnsureProducts([FromBody] EnsureProductsRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.CollectionId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "CollectionId is required." });

            try
            {
                var collection = await _projectCollectionRepository.GetByIdAsync(request.CollectionId);
                if (collection == null || collection.Status != 1)
                    return Json(new ApiResponse { success = false, message = "Collection not found." });

                var project = await _projectRepository.GetByIdAsync(collection.ProjectId, userId);
                if (project == null)
                    return Json(new ApiResponse { success = false, message = "Collection not found." });

                var blueprints = (await _blueprintRepository.GetByProjectIdAsync(collection.ProjectId)).ToList();
                var productImages = (await _productImageRepository.GetByCollectionIdAsync(request.CollectionId)).ToList();
                var imageBlueprintIds = productImages.Select(img => img.ProjectBlueprintId).Distinct().ToHashSet();
                var configuredBlueprints = blueprints.Where(bp => imageBlueprintIds.Contains(bp.Id)).ToList();

                var created = new List<object>();
                foreach (var bp in configuredBlueprints)
                {
                    var existing = await _productRepository.GetByCollectionAndBlueprintIdAsync(request.CollectionId, bp.Id);
                    if (existing == null)
                    {
                        var product = await _productRepository.CreateAsync(new ProjectCollectionProduct
                        {
                            ProjectId = collection.ProjectId,
                            CollectionId = request.CollectionId,
                            ProjectBlueprintId = bp.Id,
                            BlueprintId = bp.BlueprintId,
                            Name = bp.Name,
                            Description = bp.Description ?? "",
                            SafetyInfo = bp.SafetyInfo ?? "",
                            PricingJson = bp.PricingJson ?? "[]"
                        });
                        created.Add(product);
                    }
                }

                return Json(new ApiResponse { success = true, data = created });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        private async Task<bool> DownloadAndSaveMockupsAsync(Guid userId, int shopId, string printifyProductId, Guid projectId, Guid collectionId, Guid printifyProductEntityId)
        {
            try
            {
                var productDetails = await _printifyService.GetProductAsync(userId, shopId, printifyProductId);
                if (productDetails == null)
                {
                    Console.WriteLine($"DownloadAndSaveMockupsAsync: GetProductAsync returned null for product {printifyProductId}");
                    return false;
                }
                if (productDetails.Images == null || productDetails.Images.Count == 0)
                {
                    Console.WriteLine($"DownloadAndSaveMockupsAsync: No images found for product {printifyProductId}");
                    return false;
                }

                await _mockupRepository.DeleteByPrintifyProductIdAsync(printifyProductEntityId);

                var httpClient = _httpClientFactory.CreateClient();
                foreach (var img in productDetails.Images)
                {
                    if (string.IsNullOrWhiteSpace(img.Src)) continue;

                    var imgResponse = await httpClient.GetAsync(img.Src);
                    if (!imgResponse.IsSuccessStatusCode) continue;

                    var imgBytes = await imgResponse.Content.ReadAsByteArrayAsync();
                    if (imgBytes == null || imgBytes.Length == 0) continue;

                    var mockupId = Guid.NewGuid();
                    await _imageService.SaveProjectCollectionMockupAsync(projectId, collectionId, mockupId, imgBytes);

                    await _mockupRepository.CreateAsync(new ProjectCollectionPrintifyProductMockup
                    {
                        Id = mockupId,
                        ProjectId = projectId,
                        CollectionId = collectionId,
                        PrintifyProductId = printifyProductEntityId,
                        VariantIds = string.Join(",", img.VariantIds ?? new List<int>()),
                        Position = img.Position ?? "",
                        ImageUrl = img.Src,
                        IsDefault = img.IsDefault,
                        Status = 1,
                    });
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DownloadAndSaveMockupsAsync error: {ex.Message}");
                Console.WriteLine($"Stack: {ex.StackTrace}");
                return false;
            }
        }
    }
}
