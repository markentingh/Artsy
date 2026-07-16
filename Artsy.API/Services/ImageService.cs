using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Artsy.API.Services
{
    public interface IImageService
    {
        Task<byte[]> GetProjectCollectionArtworkAsync(Guid collectionId, Guid artworkId, int index);
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
