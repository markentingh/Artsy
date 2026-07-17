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

        async Task<byte[]> GenerateThumbnailAsync(byte[] imageData, int width)
        {
            using var image = Image.Load(imageData);
            var height = (int)Math.Round(image.Height * (width / (double)image.Width));
            image.Mutate(x => x.Resize(width, height));
            using var stream = new MemoryStream();
            image.SaveAsJpeg(stream, new JpegEncoder { Quality = 85 });
            return stream.ToArray();
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
    }
}
