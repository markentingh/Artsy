using Artsy.Data.Entities;

namespace Artsy.API.Services
{
    public class TokenCostOptions
    {
        public decimal Cost { get; set; } = 0.01m;
    }

    public interface IImageGeneration
    {
        string ModelKey { get; }
        Task<ImageGenerationResult> GenerateAsync(ImageGenerationRequest request);
        IImageTokens CreateTokenizer(ImageGenerationModel model);
    }
}
