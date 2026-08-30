using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;
using System.Text.Json;
using Artsy.API.Models.Collections;
using Artsy.API.Services;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.API.Controllers
{
    [Route("/printify/image")]
    public class PrintifyImagesController : ApiController
    {
        readonly IProjectCollectionProductImageRepository _productImageRepository;
        readonly IProjectCollectionArtworkRepository _artworkRepository;
        readonly IProjectBlueprintsRepository _projectBlueprintsRepository;
        readonly IProjectCollectionArtworkPlacementRepository _artworkPlacementRepository;
        readonly IProjectCollectionProductRepository _collectionProductRepository;
        readonly IImageService _imageService;

        public PrintifyImagesController(
            IProjectCollectionProductImageRepository productImageRepository,
            IProjectCollectionArtworkRepository artworkRepository,
            IProjectBlueprintsRepository projectBlueprintsRepository,
            IProjectCollectionArtworkPlacementRepository artworkPlacementRepository,
            IProjectCollectionProductRepository collectionProductRepository,
            IImageService imageService)
        {
            _productImageRepository = productImageRepository;
            _artworkRepository = artworkRepository;
            _projectBlueprintsRepository = projectBlueprintsRepository;
            _artworkPlacementRepository = artworkPlacementRepository;
            _collectionProductRepository = collectionProductRepository;
            _imageService = imageService;
        }

        [HttpGet("download/{collectionId}")]
        public async Task<IActionResult> Download(Guid collectionId)
        {
            try
            {
                var productImages = (await _productImageRepository.GetByCollectionIdAsync(collectionId)).ToList();
                var artworks = await _artworkRepository.GetByCollectionIdAsync(collectionId);

                var projectId = productImages.FirstOrDefault()?.ProjectId ?? artworks.FirstOrDefault()?.ProjectId ?? Guid.Empty;
                var usedItemIds = new HashSet<Guid>();
                if (projectId != Guid.Empty)
                {
                    var blueprints = await _projectBlueprintsRepository.GetByProjectIdAsync(projectId);
                    foreach (var bp in blueprints)
                    {
                        try
                        {
                            var placements = JsonSerializer.Deserialize<List<PlacementDto>>(bp.PlacementJson ?? "[]");
                            if (placements == null) continue;
                            foreach (var placement in placements)
                            {
                                var itemId = placement.GetItemId();
                                if (itemId != Guid.Empty)
                                    usedItemIds.Add(itemId);
                            }
                        }
                        catch { }
                    }
                }

                using var ms = new MemoryStream();
                using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
                {
                    // Build product name maps
                    var collectionProducts = await _collectionProductRepository.GetByCollectionIdAsync(collectionId);
                    var collectionProductNameMap = collectionProducts.ToDictionary(p => p.ProjectBlueprintId, p => p.Name ?? "");
                    var blueprintNameMap = new Dictionary<Guid, string>();
                    if (projectId != Guid.Empty)
                    {
                        var blueprints = await _projectBlueprintsRepository.GetByProjectIdAsync(projectId);
                        foreach (var bp in blueprints)
                        {
                            blueprintNameMap[bp.Id] = bp.Name ?? "Product";
                        }
                    }

                    var usedNames = new HashSet<string>();
                    foreach (var img in productImages)
                    {
                        if (!img.Generated || !img.Active) continue;
                        var imgBytes = await _imageService.GetProjectCollectionProductImageAsync(
                            img.ProjectId, img.CollectionId, img.Id);
                        if (imgBytes == null || imgBytes.Length == 0) continue;

                        // Build a descriptive file name:
                        // 1. ProjectCollectionProducts.Name
                        // 2. ProjectBlueprints.Name
                        // 3. VariantColor
                        string productName;
                        if (img.ProjectBlueprintId.HasValue && collectionProductNameMap.TryGetValue(img.ProjectBlueprintId.Value, out var cpn) && !string.IsNullOrWhiteSpace(cpn))
                            productName = cpn;
                        else if (img.ProjectBlueprintId.HasValue && blueprintNameMap.TryGetValue(img.ProjectBlueprintId.Value, out var bpn) && !string.IsNullOrWhiteSpace(bpn))
                            productName = bpn;
                        else if (!string.IsNullOrWhiteSpace(img.VariantColor))
                            productName = img.VariantColor;
                        else
                            productName = "Product";

                        var variant = !string.IsNullOrWhiteSpace(img.VariantColor) ? img.VariantColor : "";
                        var baseName = string.IsNullOrWhiteSpace(variant) || productName == variant
                            ? productName.Replace(" ", "_").Replace("/", "-")
                            : $"{productName}_{variant}".Replace(" ", "_").Replace("/", "-");
                        // Ensure unique name
                        var fileName = $"{baseName}.jpg";
                        var counter = 1;
                        while (usedNames.Contains(fileName))
                        {
                            fileName = $"{baseName}_{counter}.jpg";
                            counter++;
                        }
                        usedNames.Add(fileName);

                        var entry = archive.CreateEntry($"product-images/{fileName}");
                        using var entryStream = entry.Open();
                        await entryStream.WriteAsync(imgBytes, 0, imgBytes.Length);
                    }

                    foreach (var art in artworks)
                    {
                        if (!art.Accepted || !art.Active) continue;
                        if (!usedItemIds.Contains(art.ItemId)) continue;

                        // Skip if we already processed this artwork (dedup by artwork ID)
                        var artKey = art.Id;
                        if (usedNames.Contains($"art_{artKey}")) continue;
                        usedNames.Add($"art_{artKey}");

                        // Include the base artwork image (combined seamless image for groups, or main image)
                        byte[]? artBytes;
                        string fileName;
                        if (art.Opacity)
                        {
                            artBytes = await _imageService.GetProjectCollectionArtworkFullSizePngAsync(
                                art.ProjectId, art.CollectionId, art.ItemId, art.Id);
                            if (artBytes == null || artBytes.Length == 0)
                            {
                                artBytes = await _imageService.GetProjectCollectionArtworkPngAsync(
                                    art.ProjectId, art.CollectionId, art.ItemId, art.Id);
                            }
                            fileName = $"artworks/{art.Id}.png";
                        }
                        else
                        {
                            artBytes = await _imageService.GetProjectCollectionArtworkFullSizeAsync(
                                art.ProjectId, art.CollectionId, art.ItemId, art.Id);
                            if (artBytes == null || artBytes.Length == 0)
                            {
                                artBytes = await _imageService.GetProjectCollectionArtworkImageAsync(
                                    art.ProjectId, art.CollectionId, art.ItemId, art.Id);
                            }
                            fileName = $"artworks/{art.Id}.jpg";
                        }

                        if (artBytes != null && artBytes.Length > 0 && !usedNames.Contains(fileName))
                        {
                            usedNames.Add(fileName);
                            var entry = archive.CreateEntry(fileName);
                            using var entryStream = entry.Open();
                            await entryStream.WriteAsync(artBytes, 0, artBytes.Length);
                        }

                        // Include individual placement images (both group segments and non-group placements)
                        if (art.TotalPlacements > 0)
                        {
                            var placements = await _artworkPlacementRepository.GetByArtworkIdAsync(art.Id);
                            var seenPlacementIndices = new HashSet<int>();
                            foreach (var placement in placements)
                            {
                                if (seenPlacementIndices.Contains(placement.Index)) continue;
                                seenPlacementIndices.Add(placement.Index);

                                byte[]? placementBytes;
                                string placementFileName;

                                if (art.Opacity)
                                {
                                    placementBytes = await _imageService.GetProjectCollectionArtworkPlacementFullSizePngAsync(
                                        art.ProjectId, art.CollectionId, art.ItemId, art.Id, placement.Index);
                                    if (placementBytes == null || placementBytes.Length == 0)
                                    {
                                        placementBytes = await _imageService.GetProjectCollectionArtworkPlacementPngAsync(
                                            art.ProjectId, art.CollectionId, art.ItemId, art.Id, placement.Index);
                                    }
                                    placementFileName = $"artworks/{art.Id}_{placement.Index}.png";
                                }
                                else
                                {
                                    placementBytes = await _imageService.GetProjectCollectionArtworkPlacementFullSizeAsync(
                                        art.ProjectId, art.CollectionId, art.ItemId, art.Id, placement.Index);
                                    if (placementBytes == null || placementBytes.Length == 0)
                                    {
                                        placementBytes = await _imageService.GetProjectCollectionArtworkPlacementImageAsync(
                                            art.ProjectId, art.CollectionId, art.ItemId, art.Id, placement.Index);
                                    }
                                    placementFileName = $"artworks/{art.Id}_{placement.Index}.jpg";
                                }

                                if (placementBytes == null || placementBytes.Length == 0) continue;
                                if (usedNames.Contains(placementFileName)) continue;
                                usedNames.Add(placementFileName);
                                var pEntry = archive.CreateEntry(placementFileName);
                                using var pEntryStream = pEntry.Open();
                                await pEntryStream.WriteAsync(placementBytes, 0, placementBytes.Length);
                            }
                        }
                    }
                }

                ms.Position = 0;
                return File(ms.ToArray(), "application/zip", $"Artsy-Collection-{DateTime.UtcNow:yyyyMMdd}.zip");
            }
            catch
            {
                return NotFound();
            }
        }

        [HttpGet("product/{productImageId}")]
        public async Task<IActionResult> GetProductImage(Guid productImageId)
        {
            try
            {
                var image = await _productImageRepository.GetByIdAsync(productImageId);
                if (image == null)
                    return NotFound();

                var imgBytes = await _imageService.GetProjectCollectionProductImageAsync(
                    image.ProjectId, image.CollectionId, image.Id);
                if (imgBytes == null || imgBytes.Length == 0)
                    return NotFound();

                return File(imgBytes, "image/jpeg");
            }
            catch
            {
                return NotFound();
            }
        }

        [HttpGet("artwork/{artworkId}")]
        public async Task<IActionResult> GetArtworkImage(Guid artworkId)
        {
            try
            {
                var artwork = await _artworkRepository.GetByIdAsync(artworkId);
                if (artwork == null)
                    return NotFound();

                var imgBytes = await _imageService.GetProjectCollectionArtworkFullSizeAsync(
                    artwork.ProjectId, artwork.CollectionId, artwork.ItemId, artwork.Id);
                if (imgBytes == null || imgBytes.Length == 0)
                {
                    imgBytes = await _imageService.GetProjectCollectionArtworkImageAsync(
                        artwork.ProjectId, artwork.CollectionId, artwork.ItemId, artwork.Id);
                }
                if (imgBytes == null || imgBytes.Length == 0)
                    return NotFound();

                return File(imgBytes, "image/jpeg");
            }
            catch
            {
                return NotFound();
            }
        }
    }
}
