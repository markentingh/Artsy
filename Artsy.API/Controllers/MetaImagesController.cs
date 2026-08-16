using Microsoft.AspNetCore.Mvc;
using Artsy.API.Services;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.API.Controllers
{
    [Route("/meta/image")]
    public class MetaImagesController : ApiController
    {
        readonly IProjectCollectionProductImageRepository _productImageRepository;
        readonly IProjectCollectionArtworkRepository _artworkRepository;
        readonly IImageService _imageService;

        public MetaImagesController(
            IProjectCollectionProductImageRepository productImageRepository,
            IProjectCollectionArtworkRepository artworkRepository,
            IImageService imageService)
        {
            _productImageRepository = productImageRepository;
            _artworkRepository = artworkRepository;
            _imageService = imageService;
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

                imgBytes = await _imageService.ResizeAndCropForInstagramAsync(imgBytes);
                Response.Headers["Content-Disposition"] = "inline; filename=\"product.jpg\"";
                Response.Headers["Cache-Control"] = "public, max-age=86400";
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

                // Always prefer the _bg.jpg version if it exists (used for opacity artworks), otherwise fall back to the original
                byte[]? imgBytes = await _imageService.GetProjectCollectionArtworkJpgWithBgAsync(
                    artwork.ProjectId, artwork.CollectionId, artwork.ItemId, artwork.Id);
                if (imgBytes == null || imgBytes.Length == 0)
                {
                    imgBytes = await _imageService.GetProjectCollectionArtworkImageAsync(
                        artwork.ProjectId, artwork.CollectionId, artwork.ItemId, artwork.Id);
                }
                if (imgBytes == null || imgBytes.Length == 0)
                    return NotFound();

                imgBytes = await _imageService.ResizeAndCropForInstagramAsync(imgBytes);
                Response.Headers["Content-Disposition"] = "inline; filename=\"artwork.jpg\"";
                Response.Headers["Cache-Control"] = "public, max-age=86400";
                return File(imgBytes, "image/jpeg");
            }
            catch
            {
                return NotFound();
            }
        }
    }
}
