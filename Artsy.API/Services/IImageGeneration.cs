using Artsy.Data.Entities;

namespace Artsy.API.Services
{
    public interface IImageGeneration
    {
        string ModelKey { get; }
        Task<ImageGenerationResult> GenerateAsync(ImageGenerationRequest request);
        IImageTokens CreateTokenizer(ImageGenerationModel model);
    }
}
