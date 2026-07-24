using Microsoft.ML.Tokenizers;

namespace Artsy.API.Services
{
    public class ImageTokensForOpenAI : IImageTokens
    {
        readonly decimal _textInputPricePerMillion;
        readonly decimal _imageInputPricePerMillion;
        readonly decimal _imageOutputPricePerMillion;
        readonly Tokenizer _tokenizer;

        public ImageTokensForOpenAI(decimal textInputPricePerMillion, decimal imageInputPricePerMillion, decimal imageOutputPricePerMillion)
        {
            _textInputPricePerMillion = textInputPricePerMillion;
            _imageInputPricePerMillion = imageInputPricePerMillion;
            _imageOutputPricePerMillion = imageOutputPricePerMillion;
            _tokenizer = TiktokenTokenizer.CreateForModel("gpt-4o");
        }

        public TokenCalculationResult CalculateTokens(string prompt, int width, int height, string quality, IReadOnlyList<(int width, int height)> inputImages = null)
        {
            int inputTokens = _tokenizer.CountTokens(prompt);

            int qualityFactor = quality?.ToLowerInvariant() switch
            {
                "low" => 16,
                "medium" => 48,
                "high" => 96,
                _ => 48
            };

            int tilesW = (int)Math.Ceiling((double)width / 512);
            int tilesH = (int)Math.Ceiling((double)height / 512);
            int totalTiles = tilesW * tilesH;

            int outputTokens = qualityFactor * totalTiles;

            int imageInputTokens = 0;
            if (inputImages != null && inputImages.Count > 0)
            {
                foreach (var img in inputImages)
                {
                    int imgTilesW = (int)Math.Ceiling((double)img.width / 512);
                    int imgTilesH = (int)Math.Ceiling((double)img.height / 512);
                    imageInputTokens += qualityFactor * imgTilesW * imgTilesH;
                }
            }

            decimal inputCost = ((decimal)inputTokens / 1_000_000m) * _textInputPricePerMillion;
            decimal imageInputCost = ((decimal)imageInputTokens / 1_000_000m) * _imageInputPricePerMillion;
            decimal outputCost = ((decimal)outputTokens / 1_000_000m) * _imageOutputPricePerMillion;

            return new TokenCalculationResult
            {
                TextInputTokens = inputTokens,
                ImageInputTokens = imageInputTokens,
                ImageOutputTokens = outputTokens,
                EstimatedCostUSD = inputCost + imageInputCost + outputCost
            };
        }
    }
}
