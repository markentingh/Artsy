using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace Artsy.API.Services
{
    public interface IImageService
    {
        Task<byte[]> GetProjectCollectionArtworkAsync(Guid collectionId, Guid artworkId, int index);
        Task SaveProjectItemPreviewAsync(Guid projectId, Guid itemId, Guid previewId, byte[] imageData);
        Task<byte[]> GetProjectItemPreviewAsync(Guid projectId, Guid itemId, Guid previewId, bool thumb = false);
        Task DeleteProjectItemPreviewAsync(Guid projectId, Guid itemId, Guid previewId);
        Task SavePrintifyCatalogImageAsync(int blueprintId, int imageIndex, byte[] imageData);
        Task<byte[]> GetPrintifyCatalogImageAsync(int blueprintId, int imageIndex, bool thumb = false);
        Task<int> CountPrintifyCatalogImagesAsync(int blueprintId);
        Task SaveProjectItemReferenceAsync(Guid projectId, Guid referenceId, string extension, byte[] imageData);
        Task<byte[]> GetProjectItemReferenceAsync(Guid projectId, Guid referenceId, string extension, bool thumb = false);
        Task DeleteProjectItemReferenceAsync(Guid projectId, Guid referenceId, string extension);
        Task SaveProjectCollectionArtworkAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId, byte[] imageData);
        Task<byte[]> GetProjectCollectionArtworkImageAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId);
        Task<byte[]> GetProjectCollectionArtworkThumbAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId);
        Task<bool> GenerateProjectCollectionArtworkThumbAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId);
        Task SaveProjectCollectionArtworkFullSizeAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId, byte[] imageData);
        Task<byte[]> GetProjectCollectionArtworkFullSizeAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId);
        Task SaveProjectCollectionArtworkPngAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId, byte[] imageData);
        Task<byte[]> GetProjectCollectionArtworkPngAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId);
        Task<byte[]> GetProjectCollectionArtworkPngThumbAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId);
        Task<bool> GenerateProjectCollectionArtworkPngThumbAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId);
        Task SaveProjectCollectionArtworkFullSizePngAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId, byte[] imageData);
        Task<byte[]> GetProjectCollectionArtworkFullSizePngAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId);

        Task SaveProjectCollectionArtworkChromaAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId, byte[] imageData);
        Task<byte[]> GetProjectCollectionArtworkChromaAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId);
        Task SaveProjectCollectionArtworkJpgWithBgAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId, byte[] imageData);
        Task<byte[]> GetProjectCollectionArtworkJpgWithBgAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId);
        Task<byte[]> GetProjectCollectionArtworkJpgWithBgThumbAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId);
        Task<bool> GenerateProjectCollectionArtworkJpgWithBgThumbAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId);
        Task SaveProjectCollectionProductImageAsync(Guid projectId, Guid collectionId, Guid productImageId, byte[] imageData);
        Task<byte[]> GetProjectCollectionProductImageAsync(Guid projectId, Guid collectionId, Guid productImageId);
        Task<byte[]> GetProjectCollectionProductImageThumbAsync(Guid projectId, Guid collectionId, Guid productImageId);
        Task<bool> GenerateProjectCollectionProductImageThumbAsync(Guid projectId, Guid collectionId, Guid productImageId);
        Task SaveProjectCollectionMockupAsync(Guid projectId, Guid collectionId, Guid mockupId, byte[] imageData);
        Task<byte[]> GetProjectCollectionMockupAsync(Guid projectId, Guid collectionId, Guid mockupId);
        Task<byte[]> GetProjectCollectionMockupThumbAsync(Guid projectId, Guid collectionId, Guid mockupId);
        Task<bool> GenerateProjectCollectionMockupThumbAsync(Guid projectId, Guid collectionId, Guid mockupId);
        Task<byte[]> GetImageGenerationAsync(Guid projectId, Guid? itemId, Guid? collectionId, Guid? blueprintId, string filename);
        Task<(int width, int height)?> GetImageDimensionsAsync(byte[] imageBytes);
        Task<byte[]> ResizeImageAsync(byte[] imageData, int maxWidth);
        Task<byte[]> ResizeImageMaxAsync(byte[] imageData, int maxSize);
        Task<byte[]> ResizeAndCropForInstagramAsync(byte[] imageData);
        Task SaveCustomImageAsync(Guid appUserId, Guid imageId, string extension, byte[] imageData);
        Task<byte[]> GetCustomImageAsync(Guid appUserId, Guid imageId, string extension, bool thumb = false);
        Task DeleteCustomImageAsync(Guid appUserId, Guid imageId, string extension);
    }

    public class ImageService : IImageService
    {
        readonly IConfiguration _configuration;
        readonly IWebHostEnvironment _environment;
        readonly string _activeStorage;

        public ImageService(IConfiguration configuration, IWebHostEnvironment environment)
        {
            _configuration = configuration;
            _environment = environment;
            _activeStorage = (_configuration["Storage:Active"] ?? "filesystem").ToLowerInvariant();
        }

        public async Task<byte[]> GetProjectCollectionArtworkAsync(Guid collectionId, Guid artworkId, int index)
        {
            var fileName = $"{artworkId}_{index}.jpg";
            var relativePath = Path.Combine("projects", collectionId.ToString(), fileName);

            if (_activeStorage == "azure")
            {
                return await GetFromAzureBlobAsync(relativePath);
            }

            return await GetFromFileSystemAsync(relativePath);
        }

        public async Task SaveProjectItemPreviewAsync(Guid projectId, Guid itemId, Guid previewId, byte[] imageData)
        {
            var fileName = $"{previewId}.jpg";
            var relativePath = Path.Combine("projects", projectId.ToString(), "previews", itemId.ToString(), fileName);
            var thumbFileName = $"{previewId}_thumb.jpg";
            var thumbRelativePath = Path.Combine("projects", projectId.ToString(), "previews", itemId.ToString(), thumbFileName);
            var thumbImageData = await GenerateThumbnailAsync(imageData);

            if (_activeStorage == "azure")
            {
                await SaveToAzureBlobAsync(relativePath, imageData);
                await SaveToAzureBlobAsync(thumbRelativePath, thumbImageData);
                return;
            }

            await SaveToFileSystemAsync(relativePath, imageData);
            await SaveToFileSystemAsync(thumbRelativePath, thumbImageData);
        }

        public async Task<byte[]> GetProjectItemPreviewAsync(Guid projectId, Guid itemId, Guid previewId, bool thumb = false)
        {
            var fileName = thumb ? $"{previewId}_thumb.jpg" : $"{previewId}.jpg";
            var relativePath = Path.Combine("projects", projectId.ToString(), "previews", itemId.ToString(), fileName);

            if (_activeStorage == "azure")
            {
                var bytes = await GetFromAzureBlobAsync(relativePath);
                if (bytes.Length == 0 && thumb)
                {
                    var fullBytes = await GetFromAzureBlobAsync(Path.Combine("projects", projectId.ToString(), "previews", itemId.ToString(), $"{previewId}.jpg"));
                    if (fullBytes.Length > 0)
                    {
                        var thumbBytes = await GenerateThumbnailAsync(fullBytes);
                        await SaveToAzureBlobAsync(relativePath, thumbBytes);
                        return thumbBytes;
                    }
                }
                return bytes;
            }

            var fileBytes = await GetFromFileSystemAsync(relativePath);
            if (fileBytes.Length == 0 && thumb)
            {
                var fullBytes = await GetFromFileSystemAsync(Path.Combine("projects", projectId.ToString(), "previews", itemId.ToString(), $"{previewId}.jpg"));
                if (fullBytes.Length > 0)
                {
                    var thumbBytes = await GenerateThumbnailAsync(fullBytes);
                    await SaveToFileSystemAsync(relativePath, thumbBytes);
                    return thumbBytes;
                }
            }
            return fileBytes;
        }

        public async Task DeleteProjectItemPreviewAsync(Guid projectId, Guid itemId, Guid previewId)
        {
            var basePath = Path.Combine("projects", projectId.ToString(), "previews", itemId.ToString());
            var fileName = $"{previewId}.jpg";
            var thumbFileName = $"{previewId}_thumb.jpg";

            if (_activeStorage == "azure")
            {
                await DeleteFromAzureBlobAsync(Path.Combine(basePath, fileName));
                await DeleteFromAzureBlobAsync(Path.Combine(basePath, thumbFileName));
                return;
            }

            await DeleteFromFileSystemAsync(Path.Combine(basePath, fileName));
            await DeleteFromFileSystemAsync(Path.Combine(basePath, thumbFileName));
        }

        public async Task SavePrintifyCatalogImageAsync(int blueprintId, int imageIndex, byte[] imageData)
        {
            var fileName = $"{imageIndex}.jpg";
            var thumbFileName = $"{imageIndex}_thumb.jpg";
            var relativePath = Path.Combine("Printify", "catalog", blueprintId.ToString(), fileName);
            var thumbRelativePath = Path.Combine("Printify", "catalog", blueprintId.ToString(), thumbFileName);
            var thumbImageData = await GenerateThumbnailAsync(imageData);

            if (_activeStorage == "azure")
            {
                await SaveToAzureBlobAsync(relativePath, imageData);
                await SaveToAzureBlobAsync(thumbRelativePath, thumbImageData);
                return;
            }

            await SaveToFileSystemAsync(relativePath, imageData);
            await SaveToFileSystemAsync(thumbRelativePath, thumbImageData);
        }

        public async Task<byte[]> GetPrintifyCatalogImageAsync(int blueprintId, int imageIndex, bool thumb = false)
        {
            var fileName = thumb ? $"{imageIndex}_thumb.jpg" : $"{imageIndex}.jpg";
            var relativePath = Path.Combine("Printify", "catalog", blueprintId.ToString(), fileName);

            if (_activeStorage == "azure")
            {
                var bytes = await GetFromAzureBlobAsync(relativePath);
                if (bytes.Length == 0 && thumb)
                {
                    var fullBytes = await GetFromAzureBlobAsync(Path.Combine("Printify", "catalog", blueprintId.ToString(), $"{imageIndex}.jpg"));
                    if (fullBytes.Length > 0)
                    {
                        var thumbBytes = await GenerateThumbnailAsync(fullBytes);
                        await SaveToAzureBlobAsync(relativePath, thumbBytes);
                        return thumbBytes;
                    }
                }
                return bytes;
            }

            var fileBytes = await GetFromFileSystemAsync(relativePath);
            if (fileBytes.Length == 0 && thumb)
            {
                var fullBytes = await GetFromFileSystemAsync(Path.Combine("Printify", "catalog", blueprintId.ToString(), $"{imageIndex}.jpg"));
                if (fullBytes.Length > 0)
                {
                    var thumbBytes = await GenerateThumbnailAsync(fullBytes);
                    await SaveToFileSystemAsync(relativePath, thumbBytes);
                    return thumbBytes;
                }
            }
            return fileBytes;
        }

        public async Task<int> CountPrintifyCatalogImagesAsync(int blueprintId)
        {
            var basePath = Path.Combine("Printify", "catalog", blueprintId.ToString());
            if (_activeStorage == "azure")
            {
                var connectionString = _configuration["Storage:AzureBlob:ConnectionString"];
                var containerName = _configuration["Storage:AzureBlob:ContainerName"];
                if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(containerName))
                    return 0;

                var blobServiceClient = new BlobServiceClient(connectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
                var prefix = basePath.Replace("\\", "/") + "/";
                int count = 0;
                await foreach (var blob in containerClient.GetBlobsAsync(prefix: prefix))
                {
                    var name = blob.Name;
                    if (name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) && !name.EndsWith("_thumb.jpg", StringComparison.OrdinalIgnoreCase))
                        count++;
                }
                return count;
            }

            var dir = Path.Combine(_environment.ContentRootPath, "Content", basePath);
            if (!Directory.Exists(dir))
                return 0;

            var files = Directory.GetFiles(dir, "*.jpg");
            return files.Count(f => !f.EndsWith("_thumb.jpg", StringComparison.OrdinalIgnoreCase));
        }

        async Task<byte[]> GenerateThumbnailAsync(byte[] imageData, int size = 350)
        {
            using var image = Image.Load(imageData);
            image.Mutate(x => x
                .Resize(new ResizeOptions
                {
                    Size = new Size(size, size),
                    Mode = ResizeMode.Crop
                }));
            using var stream = new MemoryStream();
            image.SaveAsJpeg(stream, new JpegEncoder { Quality = 85 });
            return stream.ToArray();
        }

        async Task<byte[]> GeneratePngThumbnailAsync(byte[] imageData, int size = 350)
        {
            using var image = Image.Load(imageData);
            image.Mutate(x => x
                .Resize(new ResizeOptions
                {
                    Size = new Size(size, size),
                    Mode = ResizeMode.Crop
                }));
            using var stream = new MemoryStream();
            await image.SaveAsync(stream, new PngEncoder());
            return stream.ToArray();
        }

        public async Task SaveProjectItemReferenceAsync(Guid projectId, Guid referenceId, string extension, byte[] imageData)
        {
            var fileName = $"{referenceId}{extension}";
            var relativePath = Path.Combine("projects", projectId.ToString(), "references", fileName);
            var thumbFileName = $"{referenceId}_thumb.jpg";
            var thumbRelativePath = Path.Combine("projects", projectId.ToString(), "references", thumbFileName);
            var thumbImageData = await GenerateThumbnailAsync(imageData);

            if (_activeStorage == "azure")
            {
                await SaveToAzureBlobAsync(relativePath, imageData);
                await SaveToAzureBlobAsync(thumbRelativePath, thumbImageData);
                return;
            }

            await SaveToFileSystemAsync(relativePath, imageData);
            await SaveToFileSystemAsync(thumbRelativePath, thumbImageData);
        }

        public async Task<byte[]> GetProjectItemReferenceAsync(Guid projectId, Guid referenceId, string extension, bool thumb = false)
        {
            if (thumb)
            {
                var thumbFileName = $"{referenceId}_thumb.jpg";
                var thumbRelativePath = Path.Combine("projects", projectId.ToString(), "references", thumbFileName);

                byte[] thumbBytes;
                if (_activeStorage == "azure")
                    thumbBytes = await GetFromAzureBlobAsync(thumbRelativePath);
                else
                    thumbBytes = await GetFromFileSystemAsync(thumbRelativePath);

                if (thumbBytes.Length > 0)
                    return thumbBytes;

                var fullFileName = $"{referenceId}{extension}";
                var fullRelativePath = Path.Combine("projects", projectId.ToString(), "references", fullFileName);
                byte[] fullBytes;
                if (_activeStorage == "azure")
                    fullBytes = await GetFromAzureBlobAsync(fullRelativePath);
                else
                    fullBytes = await GetFromFileSystemAsync(fullRelativePath);

                if (fullBytes.Length > 0)
                {
                    var generatedThumb = await GenerateThumbnailAsync(fullBytes);
                    if (_activeStorage == "azure")
                        await SaveToAzureBlobAsync(thumbRelativePath, generatedThumb);
                    else
                        await SaveToFileSystemAsync(thumbRelativePath, generatedThumb);
                    return generatedThumb;
                }
            }

            var fileName = $"{referenceId}{extension}";
            var relativePath = Path.Combine("projects", projectId.ToString(), "references", fileName);

            if (_activeStorage == "azure")
                return await GetFromAzureBlobAsync(relativePath);

            return await GetFromFileSystemAsync(relativePath);
        }

        public async Task DeleteProjectItemReferenceAsync(Guid projectId, Guid referenceId, string extension)
        {
            var fileName = $"{referenceId}{extension}";
            var relativePath = Path.Combine("projects", projectId.ToString(), "references", fileName);
            var thumbFileName = $"{referenceId}_thumb.jpg";
            var thumbRelativePath = Path.Combine("projects", projectId.ToString(), "references", thumbFileName);

            if (_activeStorage == "azure")
            {
                await DeleteFromAzureBlobAsync(relativePath);
                await DeleteFromAzureBlobAsync(thumbRelativePath);
                return;
            }

            await DeleteFromFileSystemAsync(relativePath);
            await DeleteFromFileSystemAsync(thumbRelativePath);
        }

        async Task SaveToFileSystemAsync(string relativePath, byte[] imageData)
        {
            var filePath = Path.Combine(_environment.ContentRootPath, "Content", relativePath);
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllBytesAsync(filePath, imageData);
        }

        async Task SaveToAzureBlobAsync(string relativePath, byte[] imageData)
        {
            var connectionString = _configuration["Storage:AzureBlob:ConnectionString"];
            var containerName = _configuration["Storage:AzureBlob:ContainerName"];

            if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(containerName))
                throw new InvalidOperationException("Azure Blob storage is not configured.");

            var blobServiceClient = new BlobServiceClient(connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync();
            var blobClient = containerClient.GetBlobClient(relativePath);

            using var stream = new MemoryStream(imageData);
            await blobClient.UploadAsync(stream, overwrite: true);
        }

        async Task<byte[]> GetFromFileSystemAsync(string relativePath)
        {
            var filePath = Path.Combine(_environment.ContentRootPath, "Content", relativePath);

            if (!File.Exists(filePath))
            {
                return Array.Empty<byte>();
            }

            return await File.ReadAllBytesAsync(filePath);
        }

        async Task<byte[]> GetFromAzureBlobAsync(string relativePath)
        {
            var connectionString = _configuration["Storage:AzureBlob:ConnectionString"];
            var containerName = _configuration["Storage:AzureBlob:ContainerName"];

            if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(containerName))
            {
                return Array.Empty<byte>();
            }

            var blobServiceClient = new BlobServiceClient(connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(relativePath);

            if (!await blobClient.ExistsAsync())
            {
                return Array.Empty<byte>();
            }

            var response = await blobClient.DownloadAsync();
            using var memoryStream = new MemoryStream();
            await response.Value.Content.CopyToAsync(memoryStream);
            return memoryStream.ToArray();
        }

        async Task DeleteFromFileSystemAsync(string relativePath)
        {
            var filePath = Path.Combine(_environment.ContentRootPath, "Content", relativePath);
            if (File.Exists(filePath))
            {
                await Task.Run(() => File.Delete(filePath));
            }
        }

        async Task DeleteFromAzureBlobAsync(string relativePath)
        {
            var connectionString = _configuration["Storage:AzureBlob:ConnectionString"];
            var containerName = _configuration["Storage:AzureBlob:ContainerName"];

            if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(containerName))
                return;

            var blobServiceClient = new BlobServiceClient(connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(relativePath);

            await blobClient.DeleteIfExistsAsync();
        }

        public async Task SaveProjectCollectionArtworkAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId, byte[] imageData)
        {
            var fileName = $"{artworkId}.jpg";
            var relativePath = Path.Combine("projects", projectId.ToString(), "collections", collectionId.ToString(), itemId.ToString(), fileName);
            var thumbFileName = $"{artworkId}_thumb.jpg";
            var thumbRelativePath = Path.Combine("projects", projectId.ToString(), "collections", collectionId.ToString(), itemId.ToString(), thumbFileName);
            var thumbImageData = await GenerateThumbnailAsync(imageData);

            if (_activeStorage == "azure")
            {
                await SaveToAzureBlobAsync(relativePath, imageData);
                await SaveToAzureBlobAsync(thumbRelativePath, thumbImageData);
                return;
            }

            await SaveToFileSystemAsync(relativePath, imageData);
            await SaveToFileSystemAsync(thumbRelativePath, thumbImageData);
        }

        public async Task<byte[]> GetProjectCollectionArtworkImageAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId)
        {
            var fileName = $"{artworkId}.jpg";
            var relativePath = Path.Combine("projects", projectId.ToString(), "collections", collectionId.ToString(), itemId.ToString(), fileName);

            if (_activeStorage == "azure")
                return await GetFromAzureBlobAsync(relativePath);

            return await GetFromFileSystemAsync(relativePath);
        }

        public async Task<byte[]> GetProjectCollectionArtworkThumbAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId)
        {
            var thumbFileName = $"{artworkId}_thumb.jpg";
            var thumbRelativePath = Path.Combine("projects", projectId.ToString(), "collections", collectionId.ToString(), itemId.ToString(), thumbFileName);

            byte[] thumbBytes;
            if (_activeStorage == "azure")
                thumbBytes = await GetFromAzureBlobAsync(thumbRelativePath);
            else
                thumbBytes = await GetFromFileSystemAsync(thumbRelativePath);

            if (thumbBytes != null && thumbBytes.Length > 0)
                return thumbBytes;

            await GenerateProjectCollectionArtworkThumbAsync(projectId, collectionId, itemId, artworkId);

            if (_activeStorage == "azure")
                return await GetFromAzureBlobAsync(thumbRelativePath);
            return await GetFromFileSystemAsync(thumbRelativePath);
        }

        public async Task<bool> GenerateProjectCollectionArtworkThumbAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId)
        {
            var imageData = await GetProjectCollectionArtworkImageAsync(projectId, collectionId, itemId, artworkId);
            if (imageData == null || imageData.Length == 0)
            {
                imageData = await GetProjectCollectionArtworkFullSizeAsync(projectId, collectionId, itemId, artworkId);
                if (imageData == null || imageData.Length == 0)
                    return false;
            }

            var thumbFileName = $"{artworkId}_thumb.jpg";
            var thumbRelativePath = Path.Combine("projects", projectId.ToString(), "collections", collectionId.ToString(), itemId.ToString(), thumbFileName);
            var thumbImageData = await GenerateThumbnailAsync(imageData);

            if (_activeStorage == "azure")
            {
                await SaveToAzureBlobAsync(thumbRelativePath, thumbImageData);
                return true;
            }

            await SaveToFileSystemAsync(thumbRelativePath, thumbImageData);
            return true;
        }

        public async Task SaveProjectCollectionArtworkFullSizeAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId, byte[] imageData)
        {
            var fileName = $"{artworkId}_fullsize.jpg";
            var relativePath = Path.Combine("projects", projectId.ToString(), "collections", collectionId.ToString(), itemId.ToString(), fileName);

            if (_activeStorage == "azure")
            {
                await SaveToAzureBlobAsync(relativePath, imageData);
                return;
            }

            await SaveToFileSystemAsync(relativePath, imageData);
        }

        public async Task<byte[]> GetProjectCollectionArtworkFullSizeAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId)
        {
            var fileName = $"{artworkId}_fullsize.jpg";
            var relativePath = Path.Combine("projects", projectId.ToString(), "collections", collectionId.ToString(), itemId.ToString(), fileName);

            if (_activeStorage == "azure")
                return await GetFromAzureBlobAsync(relativePath);

            return await GetFromFileSystemAsync(relativePath);
        }

        public async Task SaveProjectCollectionArtworkPngAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId, byte[] imageData)
        {
            var fileName = $"{artworkId}.png";
            var relativePath = Path.Combine("projects", projectId.ToString(), "collections", collectionId.ToString(), itemId.ToString(), fileName);
            var thumbFileName = $"{artworkId}_thumb.png";
            var thumbRelativePath = Path.Combine("projects", projectId.ToString(), "collections", collectionId.ToString(), itemId.ToString(), thumbFileName);
            var thumbImageData = await GeneratePngThumbnailAsync(imageData);

            if (_activeStorage == "azure")
            {
                await SaveToAzureBlobAsync(relativePath, imageData);
                await SaveToAzureBlobAsync(thumbRelativePath, thumbImageData);
                return;
            }

            await SaveToFileSystemAsync(relativePath, imageData);
            await SaveToFileSystemAsync(thumbRelativePath, thumbImageData);
        }

        public async Task<byte[]> GetProjectCollectionArtworkPngAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId)
        {
            var fileName = $"{artworkId}.png";
            var relativePath = Path.Combine("projects", projectId.ToString(), "collections", collectionId.ToString(), itemId.ToString(), fileName);

            if (_activeStorage == "azure")
                return await GetFromAzureBlobAsync(relativePath);

            return await GetFromFileSystemAsync(relativePath);
        }

        public async Task<byte[]> GetProjectCollectionArtworkPngThumbAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId)
        {
            var thumbFileName = $"{artworkId}_thumb.png";
            var thumbRelativePath = Path.Combine("projects", projectId.ToString(), "collections", collectionId.ToString(), itemId.ToString(), thumbFileName);

            byte[] thumbBytes;
            if (_activeStorage == "azure")
                thumbBytes = await GetFromAzureBlobAsync(thumbRelativePath);
            else
                thumbBytes = await GetFromFileSystemAsync(thumbRelativePath);

            if (thumbBytes != null && thumbBytes.Length > 0)
                return thumbBytes;

            await GenerateProjectCollectionArtworkPngThumbAsync(projectId, collectionId, itemId, artworkId);

            if (_activeStorage == "azure")
                return await GetFromAzureBlobAsync(thumbRelativePath);
            return await GetFromFileSystemAsync(thumbRelativePath);
        }

        public async Task<bool> GenerateProjectCollectionArtworkPngThumbAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId)
        {
            var imageData = await GetProjectCollectionArtworkPngAsync(projectId, collectionId, itemId, artworkId);
            if (imageData == null || imageData.Length == 0)
            {
                imageData = await GetProjectCollectionArtworkFullSizePngAsync(projectId, collectionId, itemId, artworkId);
                if (imageData == null || imageData.Length == 0)
                    return false;
            }

            var thumbFileName = $"{artworkId}_thumb.png";
            var thumbRelativePath = Path.Combine("projects", projectId.ToString(), "collections", collectionId.ToString(), itemId.ToString(), thumbFileName);
            var thumbImageData = await GeneratePngThumbnailAsync(imageData);

            if (_activeStorage == "azure")
            {
                await SaveToAzureBlobAsync(thumbRelativePath, thumbImageData);
                return true;
            }

            await SaveToFileSystemAsync(thumbRelativePath, thumbImageData);
            return true;
        }

        public async Task SaveProjectCollectionArtworkFullSizePngAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId, byte[] imageData)
        {
            var fileName = $"{artworkId}_fullsize.png";
            var relativePath = Path.Combine("projects", projectId.ToString(), "collections", collectionId.ToString(), itemId.ToString(), fileName);

            if (_activeStorage == "azure")
            {
                await SaveToAzureBlobAsync(relativePath, imageData);
                return;
            }

            await SaveToFileSystemAsync(relativePath, imageData);
        }

        public async Task<byte[]> GetProjectCollectionArtworkFullSizePngAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId)
        {
            var fileName = $"{artworkId}_fullsize.png";
            var relativePath = Path.Combine("projects", projectId.ToString(), "collections", collectionId.ToString(), itemId.ToString(), fileName);

            if (_activeStorage == "azure")
                return await GetFromAzureBlobAsync(relativePath);

            return await GetFromFileSystemAsync(relativePath);
        }

        public async Task SaveProjectCollectionArtworkChromaAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId, byte[] imageData)
        {
            var fileName = $"{artworkId}_chroma.png";
            var relativePath = Path.Combine("projects", projectId.ToString(), "collections", collectionId.ToString(), itemId.ToString(), fileName);

            if (_activeStorage == "azure")
            {
                await SaveToAzureBlobAsync(relativePath, imageData);
                return;
            }

            await SaveToFileSystemAsync(relativePath, imageData);
        }

        public async Task<byte[]> GetProjectCollectionArtworkChromaAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId)
        {
            var fileName = $"{artworkId}_chroma.png";
            var relativePath = Path.Combine("projects", projectId.ToString(), "collections", collectionId.ToString(), itemId.ToString(), fileName);

            if (_activeStorage == "azure")
                return await GetFromAzureBlobAsync(relativePath);

            return await GetFromFileSystemAsync(relativePath);
        }

        public async Task SaveProjectCollectionArtworkJpgWithBgAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId, byte[] imageData)
        {
            var fileName = $"{artworkId}_bg.jpg";
            var relativePath = Path.Combine("projects", projectId.ToString(), "collections", collectionId.ToString(), itemId.ToString(), fileName);
            var thumbFileName = $"{artworkId}_bg_thumb.jpg";
            var thumbRelativePath = Path.Combine("projects", projectId.ToString(), "collections", collectionId.ToString(), itemId.ToString(), thumbFileName);
            var thumbImageData = await GenerateThumbnailAsync(imageData);

            if (_activeStorage == "azure")
            {
                await SaveToAzureBlobAsync(relativePath, imageData);
                await SaveToAzureBlobAsync(thumbRelativePath, thumbImageData);
                return;
            }

            await SaveToFileSystemAsync(relativePath, imageData);
            await SaveToFileSystemAsync(thumbRelativePath, thumbImageData);
        }

        public async Task<byte[]> GetProjectCollectionArtworkJpgWithBgAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId)
        {
            var fileName = $"{artworkId}_bg.jpg";
            var relativePath = Path.Combine("projects", projectId.ToString(), "collections", collectionId.ToString(), itemId.ToString(), fileName);

            if (_activeStorage == "azure")
                return await GetFromAzureBlobAsync(relativePath);

            return await GetFromFileSystemAsync(relativePath);
        }

        public async Task<byte[]> GetProjectCollectionArtworkJpgWithBgThumbAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId)
        {
            var thumbFileName = $"{artworkId}_bg_thumb.jpg";
            var thumbRelativePath = Path.Combine("projects", projectId.ToString(), "collections", collectionId.ToString(), itemId.ToString(), thumbFileName);

            byte[] thumbBytes;
            if (_activeStorage == "azure")
                thumbBytes = await GetFromAzureBlobAsync(thumbRelativePath);
            else
                thumbBytes = await GetFromFileSystemAsync(thumbRelativePath);

            if (thumbBytes != null && thumbBytes.Length > 0)
                return thumbBytes;

            await GenerateProjectCollectionArtworkJpgWithBgThumbAsync(projectId, collectionId, itemId, artworkId);

            if (_activeStorage == "azure")
                return await GetFromAzureBlobAsync(thumbRelativePath);
            return await GetFromFileSystemAsync(thumbRelativePath);
        }

        public async Task<bool> GenerateProjectCollectionArtworkJpgWithBgThumbAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId)
        {
            var imageData = await GetProjectCollectionArtworkJpgWithBgAsync(projectId, collectionId, itemId, artworkId);
            if (imageData == null || imageData.Length == 0)
                return false;

            var thumbFileName = $"{artworkId}_bg_thumb.jpg";
            var thumbRelativePath = Path.Combine("projects", projectId.ToString(), "collections", collectionId.ToString(), itemId.ToString(), thumbFileName);
            var thumbImageData = await GenerateThumbnailAsync(imageData);

            if (_activeStorage == "azure")
            {
                await SaveToAzureBlobAsync(thumbRelativePath, thumbImageData);
                return true;
            }

            await SaveToFileSystemAsync(thumbRelativePath, thumbImageData);
            return true;
        }

        public async Task SaveProjectCollectionProductImageAsync(Guid projectId, Guid collectionId, Guid productImageId, byte[] imageData)
        {
            var fileName = $"{productImageId}.jpg";
            var relativePath = Path.Combine("projects", projectId.ToString(), "collections", collectionId.ToString(), "product-images", fileName);

            if (_activeStorage == "azure")
            {
                await SaveToAzureBlobAsync(relativePath, imageData);
                return;
            }

            await SaveToFileSystemAsync(relativePath, imageData);
        }

        public async Task<byte[]> GetProjectCollectionProductImageAsync(Guid projectId, Guid collectionId, Guid productImageId)
        {
            var fileName = $"{productImageId}.jpg";
            var relativePath = Path.Combine("projects", projectId.ToString(), "collections", collectionId.ToString(), "product-images", fileName);

            if (_activeStorage == "azure")
                return await GetFromAzureBlobAsync(relativePath);

            return await GetFromFileSystemAsync(relativePath);
        }

        public async Task<byte[]> GetProjectCollectionProductImageThumbAsync(Guid projectId, Guid collectionId, Guid productImageId)
        {
            var thumbFileName = $"{productImageId}_thumb.jpg";
            var thumbRelativePath = Path.Combine("projects", projectId.ToString(), "collections", collectionId.ToString(), "product-images", thumbFileName);

            byte[] thumbBytes;
            if (_activeStorage == "azure")
                thumbBytes = await GetFromAzureBlobAsync(thumbRelativePath);
            else
                thumbBytes = await GetFromFileSystemAsync(thumbRelativePath);

            if (thumbBytes != null && thumbBytes.Length > 0)
                return thumbBytes;

            await GenerateProjectCollectionProductImageThumbAsync(projectId, collectionId, productImageId);

            if (_activeStorage == "azure")
                return await GetFromAzureBlobAsync(thumbRelativePath);
            return await GetFromFileSystemAsync(thumbRelativePath);
        }

        public async Task<bool> GenerateProjectCollectionProductImageThumbAsync(Guid projectId, Guid collectionId, Guid productImageId)
        {
            var imageData = await GetProjectCollectionProductImageAsync(projectId, collectionId, productImageId);
            if (imageData == null || imageData.Length == 0)
                return false;

            var thumbFileName = $"{productImageId}_thumb.jpg";
            var thumbRelativePath = Path.Combine("projects", projectId.ToString(), "collections", collectionId.ToString(), "product-images", thumbFileName);
            var thumbImageData = await GenerateThumbnailAsync(imageData);

            if (_activeStorage == "azure")
            {
                await SaveToAzureBlobAsync(thumbRelativePath, thumbImageData);
                return true;
            }

            await SaveToFileSystemAsync(thumbRelativePath, thumbImageData);
            return true;
        }

        public async Task SaveProjectCollectionMockupAsync(Guid projectId, Guid collectionId, Guid mockupId, byte[] imageData)
        {
            var fileName = $"{mockupId}.jpg";
            var relativePath = Path.Combine("projects", projectId.ToString(), "collections", collectionId.ToString(), "mockups", fileName);

            if (_activeStorage == "azure")
            {
                await SaveToAzureBlobAsync(relativePath, imageData);
                return;
            }

            await SaveToFileSystemAsync(relativePath, imageData);
        }

        public async Task<byte[]> GetProjectCollectionMockupAsync(Guid projectId, Guid collectionId, Guid mockupId)
        {
            var fileName = $"{mockupId}.jpg";
            var relativePath = Path.Combine("projects", projectId.ToString(), "collections", collectionId.ToString(), "mockups", fileName);

            if (_activeStorage == "azure")
                return await GetFromAzureBlobAsync(relativePath);

            return await GetFromFileSystemAsync(relativePath);
        }

        public async Task<byte[]> GetProjectCollectionMockupThumbAsync(Guid projectId, Guid collectionId, Guid mockupId)
        {
            var thumbFileName = $"{mockupId}_thumb.jpg";
            var thumbRelativePath = Path.Combine("projects", projectId.ToString(), "collections", collectionId.ToString(), "mockups", thumbFileName);

            byte[] thumbBytes;
            if (_activeStorage == "azure")
                thumbBytes = await GetFromAzureBlobAsync(thumbRelativePath);
            else
                thumbBytes = await GetFromFileSystemAsync(thumbRelativePath);

            if (thumbBytes != null && thumbBytes.Length > 0)
                return thumbBytes;

            await GenerateProjectCollectionMockupThumbAsync(projectId, collectionId, mockupId);

            if (_activeStorage == "azure")
                return await GetFromAzureBlobAsync(thumbRelativePath);
            return await GetFromFileSystemAsync(thumbRelativePath);
        }

        public async Task<bool> GenerateProjectCollectionMockupThumbAsync(Guid projectId, Guid collectionId, Guid mockupId)
        {
            var imageData = await GetProjectCollectionMockupAsync(projectId, collectionId, mockupId);
            if (imageData == null || imageData.Length == 0)
                return false;

            var thumbFileName = $"{mockupId}_thumb.jpg";
            var thumbRelativePath = Path.Combine("projects", projectId.ToString(), "collections", collectionId.ToString(), "mockups", thumbFileName);
            var thumbImageData = await GenerateThumbnailAsync(imageData);

            if (_activeStorage == "azure")
            {
                await SaveToAzureBlobAsync(thumbRelativePath, thumbImageData);
                return true;
            }

            await SaveToFileSystemAsync(thumbRelativePath, thumbImageData);
            return true;
        }

        public async Task<byte[]> GetImageGenerationAsync(Guid projectId, Guid? itemId, Guid? collectionId, Guid? blueprintId, string filename)
        {
            string relativePath;

            if (collectionId.HasValue && itemId.HasValue)
            {
                relativePath = Path.Combine("projects", projectId.ToString(), "collections", collectionId.Value.ToString(), itemId.Value.ToString(), filename);
            }
            else if (itemId.HasValue)
            {
                relativePath = Path.Combine("projects", projectId.ToString(), "previews", itemId.Value.ToString(), filename);
            }
            else if (blueprintId.HasValue)
            {
                relativePath = Path.Combine("Printify", "catalog", blueprintId.Value.ToString(), filename);
            }
            else
            {
                relativePath = Path.Combine("projects", projectId.ToString(), filename);
            }

            if (_activeStorage == "azure")
                return await GetFromAzureBlobAsync(relativePath);

            return await GetFromFileSystemAsync(relativePath);
        }

        public async Task<(int width, int height)?> GetImageDimensionsAsync(byte[] imageBytes)
        {
            if (imageBytes == null || imageBytes.Length == 0)
                return null;

            try
            {
                using var ms = new MemoryStream(imageBytes);
                using var img = await Image.LoadAsync(ms);
                return (img.Width, img.Height);
            }
            catch
            {
                return null;
            }
        }

        public async Task<byte[]> ResizeImageAsync(byte[] imageData, int maxWidth)
        {
            if (imageData == null || imageData.Length == 0)
                return imageData;

            try
            {
                using var image = Image.Load(imageData);
                if (image.Width <= maxWidth)
                    return imageData;

                var height = (int)Math.Round(image.Height * (maxWidth / (double)image.Width));
                image.Mutate(x => x.Resize(maxWidth, height));
                using var stream = new MemoryStream();
                image.SaveAsJpeg(stream, new JpegEncoder { Quality = 90 });
                return stream.ToArray();
            }
            catch
            {
                return imageData;
            }
        }

        public async Task<byte[]> ResizeImageMaxAsync(byte[] imageData, int maxSize)
        {
            if (imageData == null || imageData.Length == 0)
                return imageData;

            try
            {
                using var image = Image.Load(imageData);
                var maxDimension = Math.Max(image.Width, image.Height);
                if (maxDimension <= maxSize)
                    return imageData;

                var scale = maxSize / (double)maxDimension;
                var width = (int)Math.Round(image.Width * scale);
                var height = (int)Math.Round(image.Height * scale);
                image.Mutate(x => x.Resize(width, height));

                using var stream = new MemoryStream();
                var format = Image.DetectFormat(imageData);
                if (format is PngFormat)
                    image.SaveAsPng(stream);
                else
                    image.SaveAsJpeg(stream, new JpegEncoder { Quality = 90 });
                return stream.ToArray();
            }
            catch
            {
                return imageData;
            }
        }

        public async Task<byte[]> ResizeAndCropForInstagramAsync(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0)
                return imageData;

            using var image = Image.Load(imageData);

            var resizeWidth = 1350;
            var resizeHeight = (int)Math.Round(image.Height * (1350 / (double)image.Width));
            if (resizeHeight < 1350)
            {
                resizeHeight = 1350;
                resizeWidth = (int)Math.Round(image.Width * (1350 / (double)image.Height));
            }
            image.Mutate(x => x.Resize(resizeWidth, resizeHeight));

            var cropX = (resizeWidth - 1080) / 2;
            var cropY = (resizeHeight - 1350) / 2;
            if (cropX < 0) cropX = 0;
            if (cropY < 0) cropY = 0;
            image.Mutate(x => x.Crop(new Rectangle(cropX, cropY, 1080, 1350)));

            using var stream = new MemoryStream();
            image.SaveAsJpeg(stream, new JpegEncoder { Quality = 90 });
            return stream.ToArray();
        }

        public async Task SaveCustomImageAsync(Guid appUserId, Guid imageId, string extension, byte[] imageData)
        {
            var fileName = $"{imageId}{extension}";
            var relativePath = Path.Combine("custom-images", appUserId.ToString(), fileName);
            var isPng = extension.Equals(".png", StringComparison.OrdinalIgnoreCase);
            var thumbFileName = isPng ? $"{imageId}_thumb.png" : $"{imageId}_thumb.jpg";
            var thumbRelativePath = Path.Combine("custom-images", appUserId.ToString(), thumbFileName);
            var thumbImageData = isPng ? await GeneratePngThumbnailAsync(imageData) : await GenerateThumbnailAsync(imageData);

            if (_activeStorage == "azure")
            {
                await SaveToAzureBlobAsync(relativePath, imageData);
                await SaveToAzureBlobAsync(thumbRelativePath, thumbImageData);
                return;
            }

            await SaveToFileSystemAsync(relativePath, imageData);
            await SaveToFileSystemAsync(thumbRelativePath, thumbImageData);
        }

        public async Task<byte[]> GetCustomImageAsync(Guid appUserId, Guid imageId, string extension, bool thumb = false)
        {
            if (thumb)
            {
                var isPng = extension.Equals(".png", StringComparison.OrdinalIgnoreCase);
                var thumbFileName = isPng ? $"{imageId}_thumb.png" : $"{imageId}_thumb.jpg";
                var thumbRelativePath = Path.Combine("custom-images", appUserId.ToString(), thumbFileName);

                byte[] thumbBytes;
                if (_activeStorage == "azure")
                    thumbBytes = await GetFromAzureBlobAsync(thumbRelativePath);
                else
                    thumbBytes = await GetFromFileSystemAsync(thumbRelativePath);

                if (thumbBytes.Length > 0)
                    return thumbBytes;

                var fullFileName = $"{imageId}{extension}";
                var fullRelativePath = Path.Combine("custom-images", appUserId.ToString(), fullFileName);
                byte[] fullBytes;
                if (_activeStorage == "azure")
                    fullBytes = await GetFromAzureBlobAsync(fullRelativePath);
                else
                    fullBytes = await GetFromFileSystemAsync(fullRelativePath);

                if (fullBytes.Length > 0)
                {
                    var generatedThumb = isPng ? await GeneratePngThumbnailAsync(fullBytes) : await GenerateThumbnailAsync(fullBytes);
                    if (_activeStorage == "azure")
                        await SaveToAzureBlobAsync(thumbRelativePath, generatedThumb);
                    else
                        await SaveToFileSystemAsync(thumbRelativePath, generatedThumb);
                    return generatedThumb;
                }
            }

            var fileName = $"{imageId}{extension}";
            var relativePath = Path.Combine("custom-images", appUserId.ToString(), fileName);

            if (_activeStorage == "azure")
                return await GetFromAzureBlobAsync(relativePath);

            return await GetFromFileSystemAsync(relativePath);
        }

        public async Task DeleteCustomImageAsync(Guid appUserId, Guid imageId, string extension)
        {
            var fileName = $"{imageId}{extension}";
            var relativePath = Path.Combine("custom-images", appUserId.ToString(), fileName);
            var isPng = extension.Equals(".png", StringComparison.OrdinalIgnoreCase);
            // Delete both possible thumb extensions to cover legacy JPG thumbs
            var thumbJpgRelativePath = Path.Combine("custom-images", appUserId.ToString(), $"{imageId}_thumb.jpg");
            var thumbPngRelativePath = Path.Combine("custom-images", appUserId.ToString(), $"{imageId}_thumb.png");

            if (_activeStorage == "azure")
            {
                await DeleteFromAzureBlobAsync(relativePath);
                await DeleteFromAzureBlobAsync(thumbJpgRelativePath);
                await DeleteFromAzureBlobAsync(thumbPngRelativePath);
                return;
            }

            await DeleteFromFileSystemAsync(relativePath);
            await DeleteFromFileSystemAsync(thumbJpgRelativePath);
            await DeleteFromFileSystemAsync(thumbPngRelativePath);
        }
    }
}
