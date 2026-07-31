using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Artsy.API.Models;
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
        readonly IImageService _imageService;

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
            IImageService imageService)
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
            _imageService = imageService;
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

        [HttpPost("upload-image")]
        public async Task<IActionResult> UploadImage([FromBody] UploadProductImageRequest request)
        {
            var userId = GetUserId();
            if (userId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "Could not find user" });

            if (request.CollectionId == Guid.Empty || request.ProductImageId == Guid.Empty)
                return Json(new ApiResponse { success = false, message = "CollectionId and ProductImageId are required." });

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

                var image = await _productImageRepository.GetByIdAsync(request.ProductImageId);
                if (image == null || image.CollectionId != request.CollectionId)
                    return Json(new ApiResponse { success = false, message = "Product image not found." });

                if (!string.IsNullOrWhiteSpace(image.PrintifyImageId))
                    return Json(new ApiResponse { success = true, data = new { printifyImageId = image.PrintifyImageId } });

                var imgBytes = await _imageService.GetProjectCollectionProductImageAsync(
                    image.ProjectId, request.CollectionId, image.Id);
                if (imgBytes == null || imgBytes.Length == 0)
                    return Json(new ApiResponse { success = false, message = "Image file not found." });

                var base64 = Convert.ToBase64String(imgBytes);
                var fileName = $"{image.Id}.jpg";
                var uploadResp = await _printifyService.UploadImageAsync(userId, fileName, base64);
                if (uploadResp == null)
                    return Json(new ApiResponse { success = false, message = "Failed to upload image to Printify." });

                await _productImageRepository.SetPrintifyImageIdAsync(image.Id, uploadResp.Id);

                return Json(new ApiResponse { success = true, data = new { printifyImageId = uploadResp.Id } });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
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

                var imgBytes = await _imageService.GetProjectCollectionArtworkFullSizeAsync(
                    artwork.ProjectId, request.CollectionId, artwork.ItemId, artwork.Id);
                if (imgBytes == null || imgBytes.Length == 0)
                {
                    imgBytes = await _imageService.GetProjectCollectionArtworkImageAsync(
                        artwork.ProjectId, request.CollectionId, artwork.ItemId, artwork.Id);
                }
                if (imgBytes == null || imgBytes.Length == 0)
                    return Json(new ApiResponse { success = false, message = "Artwork file not found." });

                var base64 = Convert.ToBase64String(imgBytes);
                var fileName = $"{artwork.Id}.jpg";
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
                    .Where(img => img.ProjectBlueprintId == bp.Id && img.Accepted && img.Active && !string.IsNullOrWhiteSpace(img.PrintifyImageId))
                    .ToList();

                var printAreas = new List<PrintifyPrintAreaRequest>();
                if (!string.IsNullOrWhiteSpace(bp.PlacementJson) && collectionArtwork.Count > 0)
                {
                    try
                    {
                        var placements = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(bp.PlacementJson);
                        if (placements != null)
                        {
                            foreach (var kv in placements)
                            {
                                var position = kv.Key.ToLower();
                                var placementImages = new List<PrintifyPlaceholderImageRequest>();

                                foreach (var art in collectionArtwork)
                                {
                                    placementImages.Add(new PrintifyPlaceholderImageRequest
                                    {
                                        Id = art.PrintifyImageId,
                                        X = 0.5,
                                        Y = 0.5,
                                        Scale = 1,
                                        Angle = 0,
                                    });
                                }

                                if (placementImages.Count > 0)
                                {
                                    printAreas.Add(new PrintifyPrintAreaRequest
                                    {
                                        VariantIds = variantIds,
                                        Placeholders = new List<PrintifyPlaceholderRequest>
                                        {
                                            new PrintifyPlaceholderRequest
                                            {
                                                Position = position,
                                                Images = placementImages,
                                            }
                                        }
                                    });
                                }
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

                var images = productImages.Select(img =>
                {
                    var position = "front";
                    if (img.ProductImageId != Guid.Empty && blueprintProductImages.TryGetValue(img.ProductImageId, out var bpi))
                    {
                        if (positionByVariantColor.TryGetValue(bpi.VariantColor, out var pos))
                            position = GetPositionLabel(pos);
                    }
                    return new PrintifyProductImageRequest
                    {
                        Src = $"https://images.printify.com/{img.PrintifyImageId}.jpg",
                        VariantIds = variantIds,
                        Position = position,
                        IsDefault = false,
                    };
                }).ToList();

                var productRequest = new PrintifyProductRequest
                {
                    Title = bp.Name,
                    Description = bp.Description ?? "",
                    SafetyInformation = bp.SafetyInfo ?? "",
                    BlueprintId = bp.BlueprintId,
                    PrintProviderId = printProviderId,
                    Variants = variants,
                    PrintAreas = printAreas,
                    Images = images,
                };

                var response = await _printifyService.CreateProductAsync(userId, shopId, productRequest);
                if (response == null)
                    return Json(new ApiResponse { success = false, message = "Failed to create product on Printify." });

                var existing = await _printifyProductRepository.GetByCollectionAndProductIdAsync(request.CollectionId, product.Id);
                if (existing != null)
                {
                    existing.PrintifyProductId = response.Id;
                    existing.PrintifyShopId = response.ShopId;
                    existing.PrintifyUserId = response.UserId;
                    existing.ProviderId = response.PrintProviderId;
                    existing.Published = false;
                    existing.Status = 1;
                    await _printifyProductRepository.UpdateAsync(existing);
                    return Json(new ApiResponse { success = true, data = existing });
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
                    Status = 1
                });

                return Json(new ApiResponse { success = true, data = record });
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
                });

                return Json(new ApiResponse { success = true, data = result });
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
    }
}
