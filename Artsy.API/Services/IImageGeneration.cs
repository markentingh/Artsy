using Artsy.Data.Entities;

namespace Artsy.API.Services
{
    public interface IImageGeneration
    {
        string ModelKey { get; }
        Task<ImageGenerationResult> GenerateAsync(string imageModel, string imageModelJson, string? quality = null, string? previousResponseId = null, bool useResponsesApi = false);
        IImageTokens CreateTokenizer(ImageGenerationModel model);
    }
}
