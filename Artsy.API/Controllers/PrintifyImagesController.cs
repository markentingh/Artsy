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
        readonly IImageService _imageService;

        public PrintifyImagesController(
            IProjectCollectionProductImageRepository productImageRepository,
            IProjectCollectionArtworkRepository artworkRepository,
            IProjectBlueprintsRepository projectBlueprintsRepository,
            IImageService imageService)
        {
            _productImageRepository = productImageRepository;
            _artworkRepository = artworkRepository;
            _projectBlueprintsRepository = projectBlueprintsRepository;
            _imageService = imageService;
        }

        [HttpGet("download/{collectionId}")]
        public async Task<IActionResult> Download(Guid collectionId)
        {
            try
            {
                var productImages = await _productImageRepository.GetByCollectionIdAsync(collectionId);
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
                    foreach (var img in productImages)
                    {
                        if (!img.Accepted || !img.Active) continue;
                        var imgBytes = await _imageService.GetProjectCollectionProductImageAsync(
                            img.ProjectId, img.CollectionId, img.Id);
                        if (imgBytes == null || imgBytes.Length == 0) continue;
                        var entry = archive.CreateEntry($"product-images/{img.Id}.jpg");
                        using var entryStream = entry.Open();
                        await entryStream.WriteAsync(imgBytes, 0, imgBytes.Length);
                    }

                    foreach (var art in artworks)
                    {
                        if (!art.Accepted || !art.Active) continue;
                        if (!usedItemIds.Contains(art.ItemId)) continue;

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

                        if (artBytes == null || artBytes.Length == 0) continue;
                        var entry = archive.CreateEntry(fileName);
                        using var entryStream = entry.Open();
                        await entryStream.WriteAsync(artBytes, 0, artBytes.Length);
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
