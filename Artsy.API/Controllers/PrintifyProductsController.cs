using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
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
        readonly IProjectCollectionArtworkPlacementRepository _artworkPlacementRepository;
        readonly IProjectCollectionProductPlacementRepository _productPlacementRepository;
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
            IProjectCollectionArtworkPlacementRepository artworkPlacementRepository,
            IProjectCollectionProductPlacementRepository productPlacementRepository,
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
            _artworkPlacementRepository = artworkPlacementRepository;
            _productPlacementRepository = productPlacementRepository;
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

                // If a placement index is specified, upload the per-variant image
                if (request.PlacementIndex.HasValue && artwork.TotalPlacements > 0)
                {
                    var idx = request.PlacementIndex.Value;
                    // For seamless group placements, find by group ID + position to avoid index collisions
                    ProjectCollectionArtworkPlacement? placement = null;
                    if (request.GroupId.HasValue && !string.IsNullOrWhiteSpace(request.Position))
                    {
                        placement = await _artworkPlacementRepository.GetByArtworkIdGroupAndPositionAsync(artwork.Id, request.GroupId.Value, request.Position);
                    }
                    if (placement == null)
                    {
                        placement = await _artworkPlacementRepository.GetByArtworkIdAndIndexAsync(artwork.Id, idx);
                    }
                    if (placement == null)
                        return Json(new ApiResponse { success = false, message = $"Placement variant {idx} not found." });

                    if (!string.IsNullOrWhiteSpace(placement.PrintifyImageId))
                        return Json(new ApiResponse { success = true, data = new { printifyImageId = placement.PrintifyImageId, placementIndex = idx } });

                    byte[] variantBytes;

                    // Check if this placement belongs to a seamless group
                    if (placement.GroupId.HasValue && !string.IsNullOrWhiteSpace(placement.Position))
                    {
                        var groupId = placement.GroupId.Value;
                        var position = placement.Position;

                        if (artwork.Opacity)
                        {
                            variantBytes = await _imageService.GetProjectCollectionArtworkGroupImageFullSizePngAsync(
                                artwork.ProjectId, request.CollectionId, artwork.ItemId, artwork.Id, groupId, position);
                            if (variantBytes == null || variantBytes.Length == 0)
                            {
                                variantBytes = await _imageService.GetProjectCollectionArtworkGroupImagePngAsync(
                                    artwork.ProjectId, request.CollectionId, artwork.ItemId, artwork.Id, groupId, position);
                            }
                        }
                        else
                        {
                            variantBytes = await _imageService.GetProjectCollectionArtworkGroupImageFullSizeAsync(
                                artwork.ProjectId, request.CollectionId, artwork.ItemId, artwork.Id, groupId, position);
                            if (variantBytes == null || variantBytes.Length == 0)
                            {
                                variantBytes = await _imageService.GetProjectCollectionArtworkGroupImageAsync(
                                    artwork.ProjectId, request.CollectionId, artwork.ItemId, artwork.Id, groupId, position);
                            }
                        }
                        if (variantBytes == null || variantBytes.Length == 0)
                            return Json(new ApiResponse { success = false, message = $"Group placement {position} file not found." });

                        var variantBase64 = Convert.ToBase64String(variantBytes);
                        var variantFileName = artwork.Opacity ? $"{artwork.Id}_{position}.png" : $"{artwork.Id}_{position}.jpg";
                        var variantUploadResp = await _printifyService.UploadImageAsync(userId, variantFileName, variantBase64);
                        if (variantUploadResp == null || string.IsNullOrWhiteSpace(variantUploadResp.Id))
                            return Json(new ApiResponse { success = false, message = $"Failed to upload group placement {position} to Printify." });

                        await _artworkPlacementRepository.SetPrintifyImageIdAsync(placement.Id, variantUploadResp.Id);

                        return Json(new ApiResponse { success = true, data = new { printifyImageId = variantUploadResp.Id, placementIndex = idx } });
                    }

                    // Standard placement variant upload
                    if (artwork.Opacity)
                    {
                        variantBytes = await _imageService.GetProjectCollectionArtworkPlacementFullSizePngAsync(
                            artwork.ProjectId, request.CollectionId, artwork.ItemId, artwork.Id, idx);
                        if (variantBytes == null || variantBytes.Length == 0)
                        {
                            variantBytes = await _imageService.GetProjectCollectionArtworkPlacementPngAsync(
                                artwork.ProjectId, request.CollectionId, artwork.ItemId, artwork.Id, idx);
                        }
                    }
                    else
                    {
                        variantBytes = await _imageService.GetProjectCollectionArtworkPlacementFullSizeAsync(
                            artwork.ProjectId, request.CollectionId, artwork.ItemId, artwork.Id, idx);
                        if (variantBytes == null || variantBytes.Length == 0)
                        {
                            variantBytes = await _imageService.GetProjectCollectionArtworkPlacementImageAsync(
                                artwork.ProjectId, request.CollectionId, artwork.ItemId, artwork.Id, idx);
                        }
                    }
                    if (variantBytes == null || variantBytes.Length == 0)
                        return Json(new ApiResponse { success = false, message = $"Placement variant {idx} file not found." });

                    var stdBase64 = Convert.ToBase64String(variantBytes);
                    var stdFileName = artwork.Opacity ? $"{artwork.Id}_{idx}.png" : $"{artwork.Id}_{idx}.jpg";
                    var stdUploadResp = await _printifyService.UploadImageAsync(userId, stdFileName, stdBase64);
                    if (stdUploadResp == null || string.IsNullOrWhiteSpace(stdUploadResp.Id))
                        return Json(new ApiResponse { success = false, message = $"Failed to upload placement variant {idx} to Printify." });

                    await _artworkPlacementRepository.SetPrintifyImageIdAsync(placement.Id, stdUploadResp.Id);

                    return Json(new ApiResponse { success = true, data = new { printifyImageId = stdUploadResp.Id, placementIndex = idx } });
                }

                // Standard single-artwork upload (backward compatible)
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
                    imgBytes = ProcessImage(imgBytes, cropSettings.Value.Width, cropSettings.Value.Height, cropSettings.Value.CropX, cropSettings.Value.CropY, artwork.Opacity);

                using (var processedImage = Image.Load(imgBytes))
                {
                    artwork.Width = processedImage.Width;
                    artwork.Height = processedImage.Height;
                }

                var base64 = Convert.ToBase64String(imgBytes);
                var fileName = artwork.Opacity ? $"{artwork.Id}.png" : $"{artwork.Id}.jpg";
                var uploadResp = await _printifyService.UploadImageAsync(userId, fileName, base64);
                if (uploadResp == null || string.IsNullOrWhiteSpace(uploadResp.Id))
                    return Json(new ApiResponse { success = false, message = "Failed to upload artwork to Printify." });

                await _artworkRepository.SetPrintifyImageIdAsync(artwork.Id, uploadResp.Id);
                await _artworkRepository.UpdateAsync(artwork);

                return Json(new ApiResponse { success = true, data = new { printifyImageId = uploadResp.Id } });
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse { success = false, message = ex.Message });
            }
        }

        [HttpPost("archive-upload")]
        public async Task<IActionResult> ArchiveUpload([FromBody] ArchiveUploadRequest request)
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

                var artwork = await _artworkRepository.GetByIdAsync(request.CollectionId, request.ArtworkId);
                if (artwork == null)
                    return Json(new ApiResponse { success = false, message = "Artwork not found." });

                if (string.IsNullOrWhiteSpace(artwork.PrintifyImageId))
                    return Json(new ApiResponse { success = false, message = "Artwork is not uploaded to Printify." });

                var archived = await _printifyService.ArchiveImageAsync(userId, artwork.PrintifyImageId);
                if (!archived)
                    return Json(new ApiResponse { success = false, message = "Failed to archive image on Printify." });

                await _artworkRepository.SetPrintifyImageIdAsync(artwork.Id, "");

                return Json(new ApiResponse { success = true });
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

        private async Task SaveProductPlacementsAsync(Guid productId, List<(Guid ArtworkId, Guid? ArtworkPlacementId, int PlacementIndex, string Position, string VariantIdsJson)> placements)
        {
            // Delete existing placements for this product (re-creation scenario)
            await _productPlacementRepository.DeleteByProductIdAsync(productId);

            foreach (var (artworkId, artworkPlacementId, placementIndex, position, variantIdsJson) in placements)
            {
                await _productPlacementRepository.CreateAsync(new ProjectCollectionProductPlacement
                {
                    ProductId = productId,
                    ArtworkId = artworkId,
                    ArtworkPlacementId = artworkPlacementId,
                    Position = position,
                    VariantIds = variantIdsJson,
                    PlacementIndex = placementIndex
                });
            }
        }

        private static byte[] ProcessImage(byte[] imgBytes, int targetWidth, int targetHeight, string cropX, string cropY, bool preservePng = false)
        {
            if (preservePng || string.Equals(cropX, "fit", StringComparison.OrdinalIgnoreCase))
                return FitImage(imgBytes, targetWidth, targetHeight, cropX, cropY, preservePng);

            return CropImage(imgBytes, targetWidth, targetHeight, cropX, cropY, preservePng);
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

        private static Rectangle GetNonTransparentBounds(Image<Rgba32> image)
        {
            int left = 0;
            int right = image.Width - 1;
            int top = 0;
            int bottom = image.Height - 1;

            for (; top <= bottom; top++)
            {
                if (RowHasPixel(image, top, 0, image.Width - 1, true))
                    break;
            }

            for (; bottom >= top; bottom--)
            {
                if (RowHasPixel(image, bottom, 0, image.Width - 1, true))
                    break;
            }

            for (; left <= right; left++)
            {
                if (ColumnHasPixel(image, left, top, bottom))
                    break;
            }

            for (; right >= left; right--)
            {
                if (ColumnHasPixel(image, right, top, bottom))
                    break;
            }

            if (left > right || top > bottom)
                return Rectangle.Empty;

            return new Rectangle(left, top, right - left + 1, bottom - top + 1);
        }

        private static bool RowHasPixel(Image<Rgba32> image, int y, int xStart, int xEnd, bool horizontal)
        {
            for (int x = xStart; x <= xEnd; x++)
            {
                if (image[x, y].A > 0)
                    return true;
            }
            return false;
        }

        private static bool ColumnHasPixel(Image<Rgba32> image, int x, int yStart, int yEnd)
        {
            for (int y = yStart; y <= yEnd; y++)
            {
                if (image[x, y].A > 0)
                    return true;
            }
            return false;
        }

        private static byte[] FitImage(byte[] imgBytes, int targetWidth, int targetHeight, string cropX, string cropY, bool preservePng = false)
        {
            using var image = Image.Load<Rgba32>(imgBytes);

            if (preservePng)
            {
                var bounds = GetNonTransparentBounds(image);
                if (!bounds.IsEmpty)
                {
                    image.Mutate(ctx => ctx.Crop(bounds));
                }
            }

            var srcW = image.Width;
            var srcH = image.Height;
            var srcRatio = (double)srcW / srcH;

            int newW, newH;
            if (preservePng)
            {
                // For transparent PNG artwork, scale to the full placement height and
                // crop/letterbox horizontally so the image fits the print area height.
                newH = targetHeight;
                newW = (int)(targetHeight * srcRatio);
            }
            else
            {
                var targetRatio = (double)targetWidth / targetHeight;
                if (srcRatio > targetRatio)
                {
                    newW = targetWidth;
                    newH = (int)(targetWidth / srcRatio);
                }
                else
                {
                    newH = targetHeight;
                    newW = (int)(targetHeight * srcRatio);
                }
            }

            image.Mutate(ctx => ctx.Resize(newW, newH));

            if (preservePng)
            {
                using var canvas = new Image<Rgba32>(targetWidth, targetHeight);
                int xOffset = cropX.ToLower() switch
                {
                    "left" => 0,
                    "right" => targetWidth - newW,
                    _ => (targetWidth - newW) / 2,
                };
                canvas.Mutate(ctx => ctx.DrawImage(image, new Point(xOffset, 0), 1f));

                using var ms = new MemoryStream();
                canvas.Save(ms, new PngEncoder());
                return ms.ToArray();
            }

            using var ms2 = new MemoryStream();
            image.Save(ms2, new JpegEncoder());
            return ms2.ToArray();
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

                if (!product.Active)
                    return Json(new ApiResponse { success = false, message = "This product is not active for this collection." });

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
                    .Where(a => a.Accepted && a.Active)
                    .ToList();

                // Pre-load placement variants for all artworks in this collection
                var artworkPlacementsMap = new Dictionary<Guid, List<ProjectCollectionArtworkPlacement>>();
                foreach (var art in collectionArtwork)
                {
                    if (art.TotalPlacements > 0)
                    {
                        var placementVariants = (await _artworkPlacementRepository.GetByArtworkIdAsync(art.Id)).ToList();
                        artworkPlacementsMap[art.Id] = placementVariants;
                    }
                }

                var printAreas = new List<PrintifyPrintAreaRequest>();
                var artworkUsedInPlacements = new HashSet<Guid>();
                var savedPlacements = new List<(Guid ArtworkId, Guid? ArtworkPlacementId, int PlacementIndex, string Position, string VariantIdsJson)>();
                if (!string.IsNullOrWhiteSpace(bp.PlacementJson) && collectionArtwork.Count > 0)
                {
                    try
                    {
                        var placements = JsonSerializer.Deserialize<List<PlacementDto>>(bp.PlacementJson);
                        if (placements != null)
                        {
                            // Build a single print area with all variant_ids and one placeholder per position
                            var placeholders = new List<PrintifyPlaceholderRequest>();

                            foreach (var placement in placements)
                            {
                                if (string.IsNullOrWhiteSpace(placement.Source)) continue;

                                var itemId = placement.GetItemId();
                                if (itemId == Guid.Empty) continue;

                                var art = collectionArtwork.FirstOrDefault(a => a.ItemId == itemId);
                                if (art == null) continue;

                                artworkUsedInPlacements.Add(art.Id);

                                var position = (placement.Position ?? "").ToLower();

                                // Determine which PrintifyImageId to use: per-variant if available, else base artwork
                                string printifyImageId = art.PrintifyImageId;
                                int placementIndex = 0;
                                Guid? artworkPlacementId = null;

                                // Pattern mode: use the single artwork image with pattern settings for all placements
                                if (string.Equals(art.Design, "pattern", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (string.IsNullOrWhiteSpace(printifyImageId))
                                        continue;

                                    PrintifyPatternRequest? patternRequest = null;
                                    double patternScale = 1;
                                    if (!string.IsNullOrWhiteSpace(art.PatternJson))
                                    {
                                        try
                                        {
                                            var patternOpts = JsonSerializer.Deserialize<PatternSettingsDto>(art.PatternJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                                            if (patternOpts != null)
                                            {
                                                patternRequest = new PrintifyPatternRequest
                                                {
                                                    SpacingX = patternOpts.SpacingX,
                                                    SpacingY = patternOpts.SpacingY,
                                                    Angle = patternOpts.Angle,
                                                    Offset = patternOpts.Offset,
                                                };
                                                patternScale = patternOpts.Scale > 0 ? patternOpts.Scale : 1;
                                            }
                                        }
                                        catch { }
                                    }

                                    placeholders.Add(new PrintifyPlaceholderRequest
                                    {
                                        Position = position,
                                        Images = new List<PrintifyPlaceholderImageRequest>
                                        {
                                            new PrintifyPlaceholderImageRequest
                                            {
                                                Id = printifyImageId,
                                                X = 0.5,
                                                Y = 0.5,
                                                Scale = patternScale,
                                                Angle = 0,
                                                Pattern = patternRequest,
                                            }
                                        },
                                    });

                                    savedPlacements.Add((art.Id, null, 0, position, JsonSerializer.Serialize(variantIds)));
                                    continue;
                                }

                                if (art.TotalPlacements > 0 && artworkPlacementsMap.TryGetValue(art.Id, out var placementVariants))
                                {
                                    var (pw, ph) = placement.GetDimensions();
                                    var placementRatio = pw > 0 && ph > 0 ? (double)pw / ph : 0;

                                    // For seamless group placements, match by Position AND dimensions (case-insensitive)
                                    // This prevents matching a "front" group placement from a different blueprint
                                    var matching = placementVariants.FirstOrDefault(v =>
                                        v.GroupId.HasValue &&
                                        !string.IsNullOrWhiteSpace(v.Position) &&
                                        string.Equals(v.Position, placement.Position, StringComparison.OrdinalIgnoreCase) &&
                                        v.Width == pw && v.Height == ph &&
                                        !string.IsNullOrWhiteSpace(v.PrintifyImageId));

                                    // Fall back to position match with aspect ratio check (within 1% tolerance)
                                    if (matching == null)
                                    {
                                        matching = placementVariants.FirstOrDefault(v =>
                                            v.GroupId.HasValue &&
                                            !string.IsNullOrWhiteSpace(v.Position) &&
                                            string.Equals(v.Position, placement.Position, StringComparison.OrdinalIgnoreCase) &&
                                            v.Width > 0 && v.Height > 0 &&
                                            placementRatio > 0 &&
                                            Math.Abs((double)v.Width / v.Height - placementRatio) < 0.01 &&
                                            !string.IsNullOrWhiteSpace(v.PrintifyImageId));
                                    }

                                    // Fall back to aspect ratio matching for non-group placements
                                    if (matching == null)
                                    {
                                        matching = placementVariants.FirstOrDefault(v =>
                                        {
                                            if (v.Width <= 0 || v.Height <= 0) return false;
                                            var variantRatio = (double)v.Width / v.Height;
                                            return Math.Abs(variantRatio - placementRatio) < 0.001 && !string.IsNullOrWhiteSpace(v.PrintifyImageId);
                                        });
                                    }

                                    if (matching != null)
                                    {
                                        printifyImageId = matching.PrintifyImageId;
                                        placementIndex = matching.Index;
                                        artworkPlacementId = matching.Id;
                                    }
                                }

                                if (string.IsNullOrWhiteSpace(printifyImageId))
                                    continue;

                                double x = 0.5, y = 0.5, scale = 1;
                                if (art.Opacity || string.Equals(placement.CropX, "fit", StringComparison.OrdinalIgnoreCase))
                                {
                                    var (tw, th) = placement.GetDimensions();
                                    if (tw > 0 && th > 0 && art.Width > 0 && art.Height > 0)
                                    {
                                        var targetRatio = (double)tw / th;
                                        var srcRatio = (double)art.Width / art.Height;
                                        double fitScale = srcRatio > targetRatio
                                            ? (double)tw / art.Width
                                            : (double)th / art.Height;
                                        fitScale = Math.Min(1, fitScale);
                                        int fitH = (int)(art.Height * fitScale);
                                        var cropY = (placement.CropY ?? "").ToLower();
                                        y = cropY switch
                                        {
                                            "top" => (double)fitH / (2 * th),
                                            "bottom" => 1 - ((double)fitH / (2 * th)),
                                            _ => 0.5,
                                        };
                                        scale = fitScale;
                                    }
                                }

                                placeholders.Add(new PrintifyPlaceholderRequest
                                {
                                    Position = position,
                                    Images = new List<PrintifyPlaceholderImageRequest>
                                    {
                                        new PrintifyPlaceholderImageRequest
                                        {
                                            Id = printifyImageId,
                                            X = x,
                                            Y = y,
                                            Scale = scale,
                                            Angle = 0,
                                        }
                                    },
                                });

                                // Track placement for saving to DB
                                savedPlacements.Add((art.Id, artworkPlacementId, placementIndex, position, JsonSerializer.Serialize(variantIds)));
                            }

                            // Create a single print area with all variant_ids and all placeholders
                            if (placeholders.Count > 0)
                            {
                                printAreas.Add(new PrintifyPrintAreaRequest
                                {
                                    VariantIds = variantIds,
                                    Placeholders = placeholders,
                                });
                            }
                        }
                    }
                    catch { }
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
                };

                var requestJson = JsonSerializer.Serialize(productRequest, new JsonSerializerOptions { WriteIndented = false });

                var result = await _printifyService.CreateProductAsync(userId, shopId, productRequest);
                if (result == null)
                    return Json(new ApiResponse { success = false, message = "Failed to create product on Printify." });
                if (!result.Success)
                    return Json(new ApiResponse { success = false, message = result.Error });

                var response = result.Product;
                var responseJson = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = false });

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
                    existing.ResponseJson = responseJson;
                    await _printifyProductRepository.UpdateAsync(existing);

                    // Save placement records for this product
                    await SaveProductPlacementsAsync(product.Id, savedPlacements);

                    var mockupsDownloaded = await DownloadAndSaveMockupsAsync(userId, shopId, response.Id, collection.ProjectId, request.CollectionId, existing.Id, response.Images);

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
                    RequestJson = requestJson,
                    ResponseJson = responseJson
                });

                // Save placement records for this product
                await SaveProductPlacementsAsync(product.Id, savedPlacements);

                var mockupsDownloaded2 = await DownloadAndSaveMockupsAsync(userId, shopId, response.Id, collection.ProjectId, request.CollectionId, record.Id, response.Images);

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

                var productDetails = await _printifyService.GetProductAsync(userId, shopId, printifyProduct.PrintifyProductId);
                if (productDetails == null)
                    return Json(new ApiResponse { success = false, message = "Failed to get Printify product details." });

                var mockupsDownloaded = await DownloadAndSaveMockupsAsync(userId, shopId, printifyProduct.PrintifyProductId, collection.ProjectId, request.CollectionId, printifyProduct.Id, productDetails.Images);

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

                var response = await _printifyService.UpdateProductAsync(userId, record.PrintifyShopId, record.PrintifyProductId, (PrintifyProductRequest)null!);
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

                // Delete mockup records and images associated with this product
                try
                {
                    var mockups = await _mockupRepository.GetByPrintifyProductIdAsync(record.Id);
                    foreach (var mockup in mockups)
                    {
                        try { await _imageService.DeleteProjectCollectionMockupAsync(mockup.ProjectId, mockup.CollectionId, mockup.Id); } catch { }
                    }
                    await _mockupRepository.DeleteByPrintifyProductIdAsync(record.Id);
                }
                catch { /* mockup cleanup is best-effort */ }

                // Archive uploaded Printify images for artworks used by this blueprint and clear PrintifyImageId
                try
                {
                    var collection = await _projectCollectionRepository.GetByIdAsync(request.CollectionId);
                    if (collection != null)
                    {
                        var product = await _productRepository.GetByIdAsync(record.ProductId);
                    var blueprint = product != null ? await _blueprintRepository.GetByIdAsync(product.ProjectBlueprintId) : null;
                        if (blueprint != null && !string.IsNullOrWhiteSpace(blueprint.PlacementJson))
                        {
                            var placements = JsonSerializer.Deserialize<List<PlacementDto>>(blueprint.PlacementJson);
                            if (placements != null)
                            {
                                // Collect all item IDs used by this blueprint's placements
                                var itemIds = placements
                                    .Select(p => p.GetItemId())
                                    .Where(id => id != Guid.Empty)
                                    .Distinct()
                                    .ToHashSet();

                                var collectionArtwork = (await _artworkRepository.GetByCollectionIdAsync(request.CollectionId))
                                    .Where(a => a.Accepted && a.Active && itemIds.Contains(a.ItemId))
                                    .ToList();

                                foreach (var artwork in collectionArtwork)
                                {
                                    // Archive placement-level images
                                    if (artwork.TotalPlacements > 0)
                                    {
                                        var placementVariants = await _artworkPlacementRepository.GetByArtworkIdAsync(artwork.Id);
                                        foreach (var pv in placementVariants)
                                        {
                                            if (!string.IsNullOrWhiteSpace(pv.PrintifyImageId))
                                            {
                                                try { await _printifyService.ArchiveImageAsync(userId, pv.PrintifyImageId); } catch { }
                                                await _artworkPlacementRepository.SetPrintifyImageIdAsync(pv.Id, "");
                                            }
                                        }
                                    }

                                    // Archive the base artwork image
                                    if (!string.IsNullOrWhiteSpace(artwork.PrintifyImageId))
                                    {
                                        try { await _printifyService.ArchiveImageAsync(userId, artwork.PrintifyImageId); } catch { }
                                        await _artworkRepository.SetPrintifyImageIdAsync(artwork.Id, "");
                                    }
                                }
                            }
                        }
                    }
                }
                catch { /* archiving is best-effort; don't fail the delete */ }

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
                var collectionProducts = await _productRepository.GetByCollectionIdAsync(request.CollectionId);
                var activeBlueprintIds = collectionProducts.Where(cp => cp.Active).Select(cp => cp.ProjectBlueprintId).ToHashSet();
                var configuredBlueprints = blueprints.Where(bp => imageBlueprintIds.Contains(bp.Id) && activeBlueprintIds.Contains(bp.Id)).ToList();

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

        private async Task<bool> DownloadAndSaveMockupsAsync(Guid userId, int shopId, string printifyProductId, Guid projectId, Guid collectionId, Guid printifyProductEntityId, List<PrintifyProductImageResponse> productImages)
        {
            try
            {
                await _mockupRepository.DeleteByPrintifyProductIdAsync(printifyProductEntityId);

                if (productImages == null || productImages.Count == 0)
                {
                    Console.WriteLine($"DownloadAndSaveMockupsAsync: No images provided for product {printifyProductId}");
                    return false;
                }

                var httpClient = IPv4HttpClientHelper.CreateHttpClient(_httpClientFactory);
                foreach (var img in productImages)
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
