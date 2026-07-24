using System.Collections.Generic;

namespace Artsy.API.Services
{
    public class TokenCalculationResult
    {
        public int TextInputTokens { get; set; }
        public int ImageInputTokens { get; set; }
        public int ImageOutputTokens { get; set; }
        public decimal EstimatedCostUSD { get; set; }
    }

    public interface IImageTokens
    {
        TokenCalculationResult CalculateTokens(string prompt, int width, int height, string quality, IReadOnlyList<(int width, int height)> inputImages = null);
    }
}
