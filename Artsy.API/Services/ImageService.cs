using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace Artsy.API.Services
{
    public interface IImageService
    {
        Task<byte[]> GetProjectCollectionArtworkAsync(Guid collectionId, Guid artworkId, int index);
        Task SaveProjectItemPreviewAsync(Guid projectId, Guid itemId, Guid previewId, byte[] imageData);
        Task<byte[]> GetProjectItemPreviewAsync(Guid projectId, Guid itemId, Guid previewId, bool thumb = false);
        Task SavePrintifyCatalogImageAsync(int blueprintId, int imageIndex, byte[] imageData);
        Task<byte[]> GetPrintifyCatalogImageAsync(int blueprintId, int imageIndex, bool thumb = false);
        Task SaveProjectItemReferenceAsync(Guid projectId, Guid referenceId, string extension, byte[] imageData);
        Task<byte[]> GetProjectItemReferenceAsync(Guid projectId, Guid referenceId, string extension, bool thumb = false);
        Task DeleteProjectItemReferenceAsync(Guid projectId, Guid referenceId, string extension);
        Task SaveProjectCollectionArtworkAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId, byte[] imageData);
        Task<byte[]> GetProjectCollectionArtworkImageAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId);
        Task SaveProjectCollectionArtworkFullSizeAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId, byte[] imageData);
        Task<byte[]> GetProjectCollectionArtworkFullSizeAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId);
        Task SaveProjectCollectionProductImageAsync(Guid projectId, Guid collectionId, Guid productImageId, byte[] imageData);
        Task<byte[]> GetProjectCollectionProductImageAsync(Guid projectId, Guid collectionId, Guid productImageId);
        Task<byte[]> GetImageGenerationAsync(Guid projectId, Guid? itemId, Guid? collectionId, Guid? blueprintId, string filename);
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
            var thumbImageData = await GenerateThumbnailAsync(imageData, 250);

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
                    return await GetFromAzureBlobAsync(Path.Combine("projects", projectId.ToString(), "previews", itemId.ToString(), $"{previewId}.jpg"));
                return bytes;
            }

            var fileBytes = await GetFromFileSystemAsync(relativePath);
            if (fileBytes.Length == 0 && thumb)
                return await GetFromFileSystemAsync(Path.Combine("projects", projectId.ToString(), "previews", itemId.ToString(), $"{previewId}.jpg"));
            return fileBytes;
        }

        public async Task SavePrintifyCatalogImageAsync(int blueprintId, int imageIndex, byte[] imageData)
        {
            var fileName = $"{imageIndex}.jpg";
            var thumbFileName = $"{imageIndex}_thumb.jpg";
            var relativePath = Path.Combine("Printify", "catalog", blueprintId.ToString(), fileName);
            var thumbRelativePath = Path.Combine("Printify", "catalog", blueprintId.ToString(), thumbFileName);
            var thumbImageData = await GenerateThumbnailAsync(imageData, 250);

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
                    return await GetFromAzureBlobAsync(Path.Combine("Printify", "catalog", blueprintId.ToString(), $"{imageIndex}.jpg"));
                return bytes;
            }

            var fileBytes = await GetFromFileSystemAsync(relativePath);
            if (fileBytes.Length == 0 && thumb)
                return await GetFromFileSystemAsync(Path.Combine("Printify", "catalog", blueprintId.ToString(), $"{imageIndex}.jpg"));
            return fileBytes;
        }

        async Task<byte[]> GenerateThumbnailAsync(byte[] imageData, int width)
        {
            using var image = Image.Load(imageData);
            var height = (int)Math.Round(image.Height * (width / (double)image.Width));
            image.Mutate(x => x.Resize(width, height));
            using var stream = new MemoryStream();
            image.SaveAsJpeg(stream, new JpegEncoder { Quality = 85 });
            return stream.ToArray();
        }

        public async Task SaveProjectItemReferenceAsync(Guid projectId, Guid referenceId, string extension, byte[] imageData)
        {
            var fileName = $"{referenceId}{extension}";
            var relativePath = Path.Combine("projects", projectId.ToString(), "references", fileName);
            var thumbFileName = $"{referenceId}_thumb.jpg";
            var thumbRelativePath = Path.Combine("projects", projectId.ToString(), "references", thumbFileName);
            var thumbImageData = await GenerateThumbnailAsync(imageData, 250);

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

                if (_activeStorage == "azure")
                {
                    var thumbBytes = await GetFromAzureBlobAsync(thumbRelativePath);
                    if (thumbBytes.Length > 0) return thumbBytes;
                }
                else
                {
                    var thumbBytes = await GetFromFileSystemAsync(thumbRelativePath);
                    if (thumbBytes.Length > 0) return thumbBytes;
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

            if (_activeStorage == "azure")
            {
                await SaveToAzureBlobAsync(relativePath, imageData);
                return;
            }

            await SaveToFileSystemAsync(relativePath, imageData);
        }

        public async Task<byte[]> GetProjectCollectionArtworkImageAsync(Guid projectId, Guid collectionId, Guid itemId, Guid artworkId)
        {
            var fileName = $"{artworkId}.jpg";
            var relativePath = Path.Combine("projects", projectId.ToString(), "collections", collectionId.ToString(), itemId.ToString(), fileName);

            if (_activeStorage == "azure")
                return await GetFromAzureBlobAsync(relativePath);

            return await GetFromFileSystemAsync(relativePath);
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
    }
}
